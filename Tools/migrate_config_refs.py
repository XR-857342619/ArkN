#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
migrate_config_refs.py —— 把旧格式的 int 索引引用迁移为稳定字符串 Id 的兼容字段

旧格式 example:
  {"Id":"重构体加攻","LastingEffect":1067}
新格式（同时保留旧字段，保证旧代码兼容）:
  {"Id":"重构体加攻","LastingEffect":1067,"LastingEffectId":"重构体加攻特效"}

用法：
  python Tools/migrate_config_refs.py             # dry-run，只报告
  python Tools/migrate_config_refs.py --apply     # 写入 StreamingAssets/Data
  python Tools/migrate_config_refs.py --apply --include-bundles
                                                   # 同时写 Assets/Bundles/Data/*.txt

注意：
  - 目标 Id 顺序取自 Assets/StreamingAssets/Data/<Table>/_index.txt；
  - 只有旧 int 字段存在时才生成 Id 字段；
  - 不删除旧字段，旧版本代码/旧存档仍然可读；
  - 不会自动修复重复 Id；重复定义应由 Excel 导出校验修复。
"""
import argparse
import json
import os
import sys
from collections import defaultdict

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
DATA = os.path.join(ROOT, "Assets", "StreamingAssets", "Data")
BUNDLE_DATA = os.path.join(ROOT, "Assets", "Bundles", "Data")

# 字段 -> (目标表, 是否数组)
SCHEMA = {
    "CardData": {
        "units": ("UnitData", True),
    },
    "UnitData": {
        "Skills": ("SkillData", True),
        "MainSkill": ("SkillData", True),
        "IgnoreBuff": ("BuffData", True),
    },
    "SkillData": {
        "EnableBuff": ("BuffData", True),
        "DisableBuff": ("BuffData", True),
        "TargetEnableBuff": ("BuffData", True),
        "TargetDisableBuff": ("BuffData", True),
        "UnitLimit": ("UnitData", True),
        "Skills": ("SkillData", True),
        "ExSkills": ("SkillData", True),
        "UpgradeSkill": ("SkillData", False),
        "Bullet": ("BulletData", False),
        "Modifys": ("ModifyData", True),
        "ModifyDatas": ("ModifyData", True),
        "BuffRemoves": ("BuffData", True),
        "Buffs": ("BuffData", True),
        "ReadyEffect": ("EffectData", False),
        "StartEffect": ("EffectData", True),
        "CastEffect": ("EffectData", True),
        "HitEffect": ("EffectData", False),
        "EffectEffect": ("EffectData", False),
        "GatherEffect": ("EffectData", False),
        "LoopStartEffect": ("EffectData", False),
        "LoopCastEffect": ("EffectData", False),
    },
    "BuffData": {
        "LastingEffect": ("EffectData", False),
        "Upgrade": ("BuffData", False),
        "RelyBuff": ("BuffData", False),
    },
    "BulletData": {
        "Modifys": ("ModifyData", True),
    },
    "ModifyData": {
        "Buff": ("BuffData", False),
    },
    "ContractData": {
        "Skills": ("SkillData", True),
    },
}


def read_index(table):
    p = os.path.join(DATA, table, "_index.txt")
    if not os.path.isfile(p):
        return None
    with open(p, encoding="utf-8") as f:
        return [x.strip() for x in f if x.strip()]


def load_target_indexes():
    out = {}
    for table in set(t for fields in SCHEMA.values() for _, t, _ in ((k, v[0], v[1]) for k, v in fields.items())):
        idx = read_index(table)
        out[table] = idx
    return out


def migrate_row(row, table, target_idx, errors, where):
    changed = False
    for field, (target, is_array) in SCHEMA.get(table, {}).items():
        if field not in row:
            continue
        raw = row[field]
        if raw is None:
            continue
        idxs = raw if is_array else [raw]
        if not isinstance(idxs, list):
            idxs = [idxs]
        ids = []
        for i, v in enumerate(idxs):
            if not isinstance(v, int):
                # 已经是新格式/非整数，跳过
                break
            seq = target_idx.get(target)
            if seq is None or v < 0 or v >= len(seq):
                errors.append(f"{where} {field}[{i}] -> {target} index {v} 越界/缺 _index")
                ids.append(None)
            else:
                ids.append(seq[v])
        else:
            if is_array:
                row[field + "Ids"] = [x for x in ids if x is not None]
            else:
                if ids and ids[0] is not None:
                    row[field + "Id"] = ids[0]
            changed = True
    return changed


def process_file(path, table, target_idx, apply, errors):
    if not os.path.isfile(path):
        return 0, 0
    rows = []
    with open(path, encoding="utf-8") as f:
        for line in f:
            line = line.strip()
            if not line:
                continue
            try:
                row = json.loads(line)
            except Exception as e:
                errors.append(f"{path}: JSON 解析失败 {e}")
                continue
            before = json.dumps(row, ensure_ascii=False, sort_keys=True)
            migrate_row(row, table, target_idx, errors, f"{os.path.basename(path)} Id={row.get('Id')}")
            after = json.dumps(row, ensure_ascii=False, sort_keys=True)
            rows.append(row)
            if before != after:
                changed = 1
            else:
                changed = 0
    if apply:
        with open(path, "w", encoding="utf-8", newline="\n") as f:
            for row in rows:
                f.write(json.dumps(row, ensure_ascii=False, separators=(",", ":")) + "\n")
    return len(rows), sum(1 for r in rows if any(f + "Id" in r or f + "Ids" in r for f in SCHEMA.get(table, {})))


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--apply", action="store_true", help="实际写回文件（默认 dry-run）")
    ap.add_argument("--include-bundles", action="store_true", help="同时处理 Assets/Bundles/Data/*.txt")
    args = ap.parse_args()

    target_idx = load_target_indexes()
    for t, idx in target_idx.items():
        if idx is None:
            print(f"[warn] 缺少目标表索引: {t}")
    errors = []
    total_rows = 0
    total_with_ids = 0
    for table in SCHEMA:
        folder = os.path.join(DATA, table)
        if os.path.isdir(folder):
            for name in sorted(os.listdir(folder)):
                if not name.endswith(".txt") or name == "_index.txt":
                    continue
                n, with_ids = process_file(os.path.join(folder, name), table, target_idx, args.apply, errors)
                total_rows += n
                total_with_ids += with_ids
        if args.include_bundles:
            p = os.path.join(BUNDLE_DATA, table + ".txt")
            n, with_ids = process_file(p, table, target_idx, args.apply, errors)
            total_rows += n
            total_with_ids += with_ids

    print(f"{'APPLY' if args.apply else 'DRY-RUN'}: rows={total_rows}, rows_with_id_fields={total_with_ids}, errors={len(errors)}")
    for e in errors[:40]:
        print("  !", e)
    if len(errors) > 40:
        print(f"  ... {len(errors) - 40} more")
    return 1 if errors else 0


if __name__ == "__main__":
    sys.exit(main())
