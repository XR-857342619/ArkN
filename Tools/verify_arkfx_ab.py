#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
verify_arkfx_ab.py —— 判定一个目录里的 .ab 是否“符合本项目 ArkFx 系统”

检查项（逐条对应 ArkFx 运行时的真实要求）：
  A 文件头      : UnityFS 魔数 / 格式版本 / 构建用 Unity 版本 / 目标平台
  B 压缩算法    : blocksInfo 能否用标准 LZ4 解开；数据块 flag
                  0=None 1=LZMA 2=LZ4 3=LZ4HC → 原生 Unity 可读
                  (方舟) LZHAM 槽位=4 → 自定义 LZ4AK，原生 Unity 读不了，必须重打包
  C 可解析性    : 打补丁后用 UnityPy 打开，列出包内 prefab 名 / 外部依赖 CAB
  D 表覆盖      : EffectData.Prefab / BulletData.Model 里的名字能否在包内找到
                  （ArkFxLoader.LoadPrefabByName 就是按“prefab 名”查的）
  E 目录约定    : StreamingAssets/arkfx/<系列>/<名>_lz4.ab
                  StreamingAssets/arkfx/_deps/<8位hash>_lz4.ab
                  StreamingAssets/arkfx/fxparams.json（原版脚本参数）

用法：
  python Tools/verify_arkfx_ab.py --ab-dir "D:\\UnityWork\\解包\\Windows\\battle\\prefabs\\effects"
  python Tools/verify_arkfx_ab.py --ab-dir <dir> --project D:\\UnityWork\\zhou-master --json report.json
  python Tools/verify_arkfx_ab.py --ab-dir <dir> --header-only     # 不依赖 UnityPy

退出码：0=全部通过；1=有硬性不合规项
"""
import argparse
import collections
import json
import os
import struct
import sys

# --------------------------------------------------------------------------
# 方舟自定义 LZ4（"LZ4AK"）：token 高低半字节互换 + offset 由大端改小端。
# 出处：Arknights v2.5.04+ 起 UnityFS 数据块 flag=4(LZHAM 槽位) 实际使用该算法。
# --------------------------------------------------------------------------
def _read_extra_length(data, cur, end):
    n = 0
    while cur < end:
        b = data[cur]
        cur += 1
        n += b
        if b != 0xFF:
            break
    return n, cur


def decompress_lz4ak(compressed_data, uncompressed_size):
    import lz4.block
    ip = op = 0
    buf = bytearray(compressed_data)
    size = len(buf)
    while ip < size:
        literal_length = buf[ip] & 0xF
        match_length = (buf[ip] >> 4) & 0xF
        buf[ip] = (literal_length << 4) | match_length
        ip += 1
        if literal_length == 0xF:
            n, ip = _read_extra_length(buf, ip, size)
            literal_length += n
        ip += literal_length
        op += literal_length
        if op >= uncompressed_size:
            break
        offset = (buf[ip] << 8) | buf[ip + 1]
        buf[ip] = offset & 0xFF
        buf[ip + 1] = (offset >> 8) & 0xFF
        ip += 2
        if match_length == 0xF:
            n, ip = _read_extra_length(buf, ip, size)
            match_length += n
        match_length += 4
        op += match_length
    return lz4.block.decompress(bytes(buf), uncompressed_size)


# --------------------------------------------------------------------------
# A/B：纯 Python 解析 UnityFS 头 + blocksInfo（不需要 UnityPy）
# --------------------------------------------------------------------------
def parse_unityfs(path):
    with open(path, "rb") as f:
        raw = f.read()
    if not raw.startswith(b"UnityFS\x00"):
        return {"error": "不是 UnityFS 包（魔数不符）"}
    i = 8
    fmt = struct.unpack(">I", raw[i:i + 4])[0]
    i += 4

    def cstr(pos):
        end = raw.index(b"\x00", pos)
        return raw[pos:end].decode("latin1"), end + 1

    engine_min, i = cstr(i)
    engine_rev, i = cstr(i)
    size, cinfo, uinfo, flags = struct.unpack(">qIII", raw[i:i + 20])
    i += 20
    # 两种布局都要支持：
    #   flags & 0x80 (BlocksInfoAtTheEnd) → blocksInfo 在文件末尾
    #   flags & 0x200 (BlockInfoNeedPaddingAtStart) → blocksInfo 前按 16 字节对齐
    if flags & 0x80:
        pos = len(raw) - cinfo
    elif fmt >= 7 or (flags & 0x200):
        pos = i + ((16 - i % 16) % 16)      # Unity 对 format>=7 一律 16 字节对齐
    else:
        pos = i
    out = {
        "format": fmt,
        "engine": engine_min,
        "engine_rev": engine_rev,
        "declared_size": size,
        "actual_size": len(raw),
        "archive_flags": flags,
        "archive_comp": flags & 0x3F,
        "blocks": [],
        "nodes": [],
    }
    try:
        import lz4.block
        info = lz4.block.decompress(raw[pos:pos + cinfo], uncompressed_size=uinfo)
    except Exception as e:                                    # noqa: BLE001
        out["error"] = f"blocksInfo 解压失败（可能被加密/非标准）：{e}"
        return out
    k = 16                                                    # 16 字节 hash
    nblk = struct.unpack(">i", info[k:k + 4])[0]
    k += 4
    for _ in range(nblk):
        u, c = struct.unpack(">II", info[k:k + 8])
        fl = struct.unpack(">H", info[k + 8:k + 10])[0]
        k += 10
        out["blocks"].append({"u": u, "c": c, "flags": fl})
    nnode = struct.unpack(">i", info[k:k + 4])[0]
    k += 4
    for _ in range(nnode):
        k += 20                                               # offset/size/flags
        end = info.index(b"\x00", k)
        out["nodes"].append(info[k:end].decode("latin1"))
        k = end + 1
    return out


BLOCK_FLAG_NAME = {0: "None", 1: "LZMA", 2: "LZ4", 3: "LZ4HC", 4: "LZHAM槽位(=方舟LZ4AK)"}


# --------------------------------------------------------------------------
# C：用 UnityPy(+LZ4AK 补丁) 打开，取 prefab 名与外部依赖
# --------------------------------------------------------------------------
def unitypy_probe(path):
    import UnityPy
    from UnityPy.enums.BundleFile import CompressionFlags
    from UnityPy.helpers import CompressionHelper
    CompressionHelper.DECOMPRESSION_MAP[CompressionFlags.LZHAM] = decompress_lz4ak

    env = UnityPy.load(path)
    objs = list(env.objects)
    container = list(getattr(env, "container", {}) or {})
    prefabs = sorted({
        os.path.splitext(os.path.basename(c))[0]
        for c in container if c.lower().endswith(".prefab")
    })
    ext = set()
    for _name, sf in getattr(env.file, "files", {}).items():
        for e in (getattr(sf, "externals", None) or []):
            p = getattr(e, "path", "") or ""
            if p.startswith("archive:/"):
                ext.add(p.split("/")[1])
    return {
        "objects": len(objs),
        "types": dict(collections.Counter(o.type.name for o in objs)),
        "prefabs": prefabs,
        "ext_deps": sorted(ext),
    }


# --------------------------------------------------------------------------
# D：工程表里的特效名
# --------------------------------------------------------------------------
def load_table_names(project):
    names = {"effect": set(), "bullet": set()}
    base = os.path.join(project, "Assets", "StreamingAssets", "Data")
    for key, sub, field in (("effect", "EffectData", "Prefab"), ("bullet", "BulletData", "Model")):
        d = os.path.join(base, sub)
        if not os.path.isdir(d):
            continue
        for f in os.listdir(d):
            if not f.endswith(".txt"):
                continue
            with open(os.path.join(d, f), encoding="utf-8", errors="ignore") as fh:
                for line in fh:
                    try:
                        v = json.loads(line).get(field)
                    except Exception:                          # noqa: BLE001
                        continue
                    if v:
                        names[key].add(v)
    return names


def project_unity_version(project):
    p = os.path.join(project, "ProjectSettings", "ProjectVersion.txt")
    if not os.path.isfile(p):
        return None
    for line in open(p, encoding="utf-8"):
        if line.startswith("m_EditorVersion:"):
            return line.split(":", 1)[1].strip()
    return None


# --------------------------------------------------------------------------
def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--ab-dir", required=True)
    ap.add_argument("--project", default=os.path.abspath(os.path.join(os.path.dirname(__file__), "..")))
    ap.add_argument("--header-only", action="store_true", help="只做格式/压缩检查，不 import UnityPy")
    ap.add_argument("--limit", type=int, default=0)
    ap.add_argument("--json", default=None, help="把完整结果写入 JSON")
    args = ap.parse_args()

    files = sorted(
        os.path.join(dp, f)
        for dp, _dn, fn in os.walk(args.ab_dir) for f in fn if f.lower().endswith(".ab")
    )
    if args.limit:
        files = files[:args.limit]
    if not files:
        print(f"[!] {args.ab_dir} 下没有 .ab 文件")
        return 1

    print(f"扫描目录 : {args.ab_dir}")
    print(f"文件数量 : {len(files)}")
    print("-" * 78)

    rows = []
    for f in files:
        r = {"file": os.path.relpath(f, args.ab_dir), "bytes": os.path.getsize(f)}
        r.update(parse_unityfs(f))
        if not args.header_only and "error" not in r:
            try:
                r.update(unitypy_probe(f))
            except Exception as e:                             # noqa: BLE001
                r["probe_error"] = f"{type(e).__name__}: {e}"
        rows.append(r)

    # ---------------- 汇总 ----------------
    def cnt(fn):
        return collections.Counter(fn(r) for r in rows)

    print("[A] 格式 / 引擎版本")
    print("    UnityFS 魔数 :", cnt(lambda r: "OK" if "error" not in r or "blocksInfo" not in str(r.get("error")) else "BAD"))
    print("    构建引擎版本 :", dict(cnt(lambda r: r.get("engine_rev"))))
    pv = project_unity_version(args.project)
    print(f"    工程版本     : {pv}")
    if pv and any(r.get("engine_rev") and r["engine_rev"] != pv for r in rows):
        print("    [!] 构建引擎与工程版本不一致：原生 AssetBundle 不保证向前兼容")

    print("[B] 压缩算法（决定原生 Unity 能不能直接 LoadFromFile）")
    blk = collections.Counter()
    for r in rows:
        for b in r.get("blocks", []):
            blk[b["flags"]] += 1
    for k, v in sorted(blk.items()):
        print(f"    数据块 flag={k} ({BLOCK_FLAG_NAME.get(k, '?')}) : {v} 块")
    native = all(k in (0, 1, 2, 3) for k in blk)
    print("    结论:", "原生 Unity 可读" if native else
          "含方舟 LZ4AK 自定义压缩 → 原生 Unity/AssetBundle.LoadFromFile 读不了，必须重打包")

    if not args.header_only:
        ok = [r for r in rows if "prefabs" in r]
        prefabs = set()
        deps = collections.Counter()
        for r in ok:
            prefabs.update(r["prefabs"])
            deps.update(r["ext_deps"])
        print("[C] 内容")
        print(f"    可解析包     : {len(ok)}/{len(rows)}")
        print(f"    包内 prefab  : {len(prefabs)} 个（去重）")
        print(f"    外部依赖 CAB : {len(deps)} 个")

        names = load_table_names(args.project)
        for key, label in (("effect", "EffectData.Prefab"), ("bullet", "BulletData.Model")):
            if not names[key]:
                continue
            hit = [n for n in names[key] if n in prefabs]
            miss = sorted(n for n in names[key] if n not in prefabs)
            print(f"    {label:<18}: 表内 {len(names[key])} 个，命中 {len(hit)}，缺失 {len(miss)}")
            if miss:
                print(f"      缺失样例: {miss[:8]}")

    print("[E] ArkFx 目录约定（目标形态 StreamingAssets/arkfx/...）")
    arkfx = os.path.join(args.project, "Assets", "StreamingAssets", "arkfx")
    print(f"    {arkfx} 存在: {os.path.isdir(arkfx)}")
    if os.path.isdir(arkfx):
        n_lz4 = sum(1 for dp, _d, fn in os.walk(arkfx) for f in fn if f.endswith("_lz4.ab"))
        print(f"    *_lz4.ab 文件数: {n_lz4}")
        print(f"    fxparams.json  : {os.path.isfile(os.path.join(arkfx, 'fxparams.json'))}")
        print(f"    _deps 目录     : {os.path.isdir(os.path.join(arkfx, '_deps'))}")

    if args.json:
        with open(args.json, "w", encoding="utf-8") as fh:
            json.dump(rows, fh, ensure_ascii=False, indent=1)
        print(f"\n完整结果已写入 {args.json}")

    bad = (not native) or (not args.header_only and any("error" in r or "probe_error" in r for r in rows))
    print("\n判定:", "✗ 不能直接交给 ArkFx 运行时使用" if bad else "✓ 符合 ArkFx 包要求")
    return 1 if bad else 0


if __name__ == "__main__":
    sys.exit(main())
