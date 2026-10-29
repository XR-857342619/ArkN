#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
verify_config_id_support.py —— 配置字符串 Id 迁移/导表校验的静态检查

用法：
    python Tools/verify_config_id_support.py
退出码：0=通过；1=失败
"""
import glob
import json
import os
import sys

try:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
except Exception:
    pass

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
FAILS = []


def read(rel):
    p = os.path.join(ROOT, rel)
    if not os.path.isfile(p):
        FAILS.append("missing " + rel)
        return ""
    with open(p, encoding="utf-8", errors="ignore") as f:
        return f.read()


def check(name, cond, detail=""):
    print(("PASS  " if cond else "FAIL  ") + name + (f"  [{detail}]" if detail else ""))
    if not cond:
        FAILS.append(name)


def main():
    db = read("Assets/Scripts/Database.cs")
    attr = read("Assets/Scripts/Config/ConfigRefAttribute.cs")
    helper = read("Assets/Scripts/Helper/ExcelHelper.cs")
    editor = read("Assets/Editor/ExcelEditor/ExcelExportEditor.cs")
    buff = read("Assets/Scripts/Core/Buff/Buff.cs")
    effect_mgr = read("Assets/Scripts/VFX/EffectManager.cs")

    check("存在 ConfigRefAttribute", "class ConfigRefAttribute" in attr)
    check("Database 有 byId / indexById", "byId" in db and "indexById" in db)
    check("Database 有字符串 Id 主入口", "public T Get<T>(string id)" in db)
    check("Database 有字符串引用回填", "BindStringReferences" in db and "ConfigRefAttribute" in db)
    check("Database 对重复 Id 报错", "重复 Id" in db)
    check("ExcelHelper 有导表校验", "ValidateExcelFiles" in helper and "重复 ID" in helper)
    check("ExcelExportEditor 有校验弹窗", "配置校验失败" in editor and "DisplayDialog" in editor)
    check("Excel 导出会生成/写出 Id 字段", "AppendReferenceIdFields" in helper and "ConfigRef" in editor)
    check("Buff 优先使用 LastingEffectId", "LastingEffectId" in buff)
    check("EffectManager 支持字符串 Id", "GetEffect(string effectId)" in effect_mgr)

    # 当前已迁移数据中的关键链路
    target = {"重构体中毒", "重构体加攻"}
    found = {}
    for f in glob.glob(os.path.join(ROOT, "Assets/StreamingAssets/Data/BuffData/*.txt")):
        if os.path.basename(f).startswith("_index"):
            continue
        for line in open(f, encoding="utf-8"):
            try:
                o = json.loads(line)
            except Exception:
                continue
            if o.get("Id") in target and o["Id"] not in found:
                found[o["Id"]] = o
    check("重构体关键 Buff 数据已含 LastingEffectId",
          all("LastingEffectId" in found.get(k, {}) for k in target),
          str({k: found.get(k, {}).get("LastingEffectId") for k in target}))

    # 当前 Config 类有一定数量的 [ConfigRef]
    refs = 0
    for f in glob.glob(os.path.join(ROOT, "Assets/Scripts/Config/*.cs")):
        refs += open(f, encoding="utf-8", errors="ignore").read().count("[ConfigRef")
    check("Config 类至少有引用字段标注", refs >= 20, f"count={refs}")

    print("-" * 72)
    if FAILS:
        print("判定: [FAIL] " + str(len(FAILS)) + " 项失败")
        for x in FAILS:
            print("  - " + x)
        return 1
    print("判定: [OK] 字符串 Id 兼容层/导出校验静态检查全部通过")
    return 0


if __name__ == "__main__":
    sys.exit(main())
