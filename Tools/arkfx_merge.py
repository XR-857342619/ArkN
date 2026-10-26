#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
arkfx_merge.py —— 方舟特效 AB → ArkFx 运行时包（方案 A：只重打包，不生成 fxparams.json）

做四件事：
  1. 扫描源目录的方舟 AB，读出每个包里的 prefab 名 + 外部依赖 CAB（LZ4AK 解压）
  2. 按工程 EffectData.Prefab / BulletData.Model 挑选"游戏实际用到的包"（--all 则全量）
  3. 把主包和它依赖的传递闭包一起**重打包成标准 LZ4**（原生 Unity 可 LoadFromFile）
  4. 按 ArkFxLoader 的目录约定归档：
         <out>/<series>/<原名>_lz4.ab
         <out>/_deps/<8位hash>_lz4.ab
     并输出 manifest + prefab 清单

依赖：pip install UnityPy lz4

用法：
  # 只打包表里用得到的（推荐，76 个包 / 24MB）
  python Tools/arkfx_merge.py
  # 全量 570 个包
  python Tools/arkfx_merge.py --all
  # 先看计划不写盘
  python Tools/arkfx_merge.py --dry-run
  # 指定源/输出
  python Tools/arkfx_merge.py --src "D:\\...\\effects" --out Assets/StreamingAssets/arkfx
"""
import argparse
import collections
import json
import os
import re
import struct
import sys
import time

# --------------------------------------------------------------------------
# 方舟自定义 LZ4（LZ4AK）：token 高低半字节互换 + offset 大端→小端
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


_ORIG_MAP = None


def patch_unitypy(on=True):
    """on=True: 让 UnityPy 能读方舟 LZ4AK；on=False: 还原成原生行为（用于校验输出）"""
    global _ORIG_MAP
    from UnityPy.enums.BundleFile import CompressionFlags
    from UnityPy.helpers import CompressionHelper
    if _ORIG_MAP is None:
        _ORIG_MAP = dict(CompressionHelper.DECOMPRESSION_MAP)
    CompressionHelper.DECOMPRESSION_MAP.clear()
    CompressionHelper.DECOMPRESSION_MAP.update(_ORIG_MAP)
    if on:
        CompressionHelper.DECOMPRESSION_MAP[CompressionFlags.LZHAM] = decompress_lz4ak


# --------------------------------------------------------------------------
# 工程表
# --------------------------------------------------------------------------
def load_wanted(project):
    wanted = {}
    base = os.path.join(project, "Assets", "StreamingAssets", "Data")
    for sub, field in (("EffectData", "Prefab"), ("BulletData", "Model")):
        d = os.path.join(base, sub)
        if not os.path.isdir(d):
            continue
        for f in sorted(os.listdir(d)):
            if not f.endswith(".txt"):
                continue
            with open(os.path.join(d, f), encoding="utf-8", errors="ignore") as fh:
                for line in fh:
                    try:
                        v = json.loads(line).get(field)
                    except Exception:                                  # noqa: BLE001
                        continue
                    if v:
                        wanted[v] = f"{sub}/{f}"
    return wanted


# --------------------------------------------------------------------------
# 单包探测
# --------------------------------------------------------------------------
UNITY_TARGET = "2021.2.10f1"

_PROBE_CACHE = {}


def _cache_file(project):
    return os.path.join(project, "Library", "arkfx_probe_cache.json")


def load_probe_cache(project):
    try:
        with open(_cache_file(project), encoding="utf-8") as fh:
            return json.load(fh)
    except Exception:                                               # noqa: BLE001
        return {}


def save_probe_cache(project, cache):
    try:
        os.makedirs(os.path.dirname(_cache_file(project)), exist_ok=True)
        with open(_cache_file(project), "w", encoding="utf-8") as fh:
            json.dump(cache, fh, ensure_ascii=False)
    except Exception:                                               # noqa: BLE001
        pass


def probe(path, cache=None):
    """返回 {prefabs:[], deps:[], engine:str}；失败返回 {'error':...}"""
    import UnityPy
    if cache is not None:
        try:
            key = f"{path}|{os.path.getsize(path)}|{int(os.path.getmtime(path))}"
        except OSError:
            key = path
        hit = cache.get(key)
        if hit is not None:
            return hit
    patch_unitypy(True)
    try:
        env = UnityPy.load(path)
        container = list(getattr(env, "container", {}) or [])
        prefabs = sorted({
            os.path.splitext(os.path.basename(c))[0]
            for c in container if c.lower().endswith(".prefab")
        })
        deps = set()
        for _n, sf in getattr(env.file, "files", {}).items():
            for e in (getattr(sf, "externals", None) or []):
                p = getattr(e, "path", "") or ""
                if p.startswith("archive:/"):
                    deps.add(p.split("/")[1])
        engine = getattr(env.file, "version_engine", "") or ""
        res = {"prefabs": prefabs, "deps": sorted(deps), "engine": engine}
        if cache is not None:
            cache[key] = res
        return res
    except Exception as e:                                          # noqa: BLE001
        return {"error": f"{type(e).__name__}: {e}"}


CAB_RE = re.compile(rb"CAB-[0-9a-fA-F]{32}")


def bundle_cab_names(path):
    """读 UnityFS 头 + blocksInfo，返回包内 SerializedFile 名（CAB-xxx[/.resS]）。
    这是判断"哪个文件真正提供某个 CAB"的唯一可靠依据——
    不能用原始字节搜 CAB 串：引用者的 externals 也会以 LZ4 literal 形式留在文件里。"""
    import lz4.block
    try:
        sz = os.path.getsize(path)
        with open(path, "rb") as f:
            head = f.read(512)
            if not head.startswith(b"UnityFS\x00"):
                return []
            i = 12                                       # 跳过 magic + format
            j = head.index(b"\x00", i); i = j + 1        # engine
            j = head.index(b"\x00", i); i = j + 1        # revision
            _size, cinfo, uinfo, flags = struct.unpack(">qIII", head[i:i + 20])
            i += 20
            pos = sz - cinfo if flags & 0x80 else i + ((16 - i % 16) % 16)
            f.seek(pos)
            comp = f.read(cinfo)
        for fn in (lz4.block.decompress, decompress_lz4ak):
            try:
                info = fn(comp, uinfo)
                break
            except Exception:                                # noqa: BLE001
                continue
        else:
            return []
        k = 16
        nblk = struct.unpack(">i", info[k:k + 4])[0]
        k += 4 + 10 * nblk
        nnode = struct.unpack(">i", info[k:k + 4])[0]
        k += 4
        out = []
        for _ in range(nnode):
            k += 20
            e = info.index(b"\x00", k)
            out.append(info[k:e].decode("latin1"))
            k = e + 1
        return out
    except Exception:                                        # noqa: BLE001
        return []


def index_cab_providers(dep_root, skip_dirs=()):
    """一趟扫完 dep_root 下所有 .ab 的 blocksInfo，建立 CAB → [提供它的文件]"""
    skip = {os.path.normcase(d) for d in skip_dirs}
    idx = collections.defaultdict(list)
    total = 0
    for dp, dns, fns in os.walk(dep_root):
        if os.path.normcase(dp) in skip:
            continue
        for f in fns:
            if not f.lower().endswith((".ab", ".bin")):
                continue
            p = os.path.join(dp, f)
            total += 1
            for cab in bundle_cab_names(p):
                if cab.endswith(".resS"):
                    continue
                if p not in idx[cab]:
                    idx[cab].append(p)
    return idx, total


def bundle_object_types(path):
    """返回包内对象类型集合（用于判断是不是纯脚本包）"""
    import UnityPy
    patch_unitypy(True)
    env = UnityPy.load(path)
    return {o.type.name for o in env.objects}


def classify_deps(path):
    """统计包内每个外部 CAB 被哪些类型的对象引用 —— 用来判断缺失依赖是否致命"""
    import UnityPy
    patch_unitypy(True)
    try:
        env = UnityPy.load(path)
    except Exception:                                        # noqa: BLE001
        return {}
    res = collections.defaultdict(collections.Counter)

    def walk(v, out):
        if isinstance(v, dict):
            fid = v.get("m_FileID")
            if fid and "m_PathID" in v and fid not in out:
                out.add(fid)
            for x in v.values():
                walk(x, out)
        elif isinstance(v, (list, tuple)):
            for x in v:
                walk(x, out)

    for _n, sf in getattr(env.file, "files", {}).items():
        exts = [(getattr(e, "path", "") or "").split("/")[-1]
                for e in (getattr(sf, "externals", None) or [])]
        for o in env.objects:
            try:
                tree = o.read_typetree()
            except Exception:                                # noqa: BLE001
                continue
            s = set()
            walk(tree, s)
            for fid in s:
                if 1 <= fid <= len(exts):
                    res[exts[fid - 1]][o.type.name] += 1
    return {k: dict(v) for k, v in res.items()}


def pick_provider(paths, root=None):
    """同一 CAB 可能有多份提供者（anon/ 下常留多个历史版本），取修改时间最新的"""
    def score(p):
        try:
            mt = os.path.getmtime(p)
        except OSError:
            mt = 0
        return (mt, os.path.getsize(p))
    return sorted(paths, key=score)[-1]


# --------------------------------------------------------------------------
# 重打包
# --------------------------------------------------------------------------

# --------------------------------------------------------------------------
# UnityFS 容器写出（Unity 2021.2 兼容版）
#
# UnityPy 的 save(packer="lz4") 写出的是 format 8 + blocksInfo 在文件尾 + 无对齐，
# Unity 2021.2 的加载器会直接报 "Unable to read header from archive file"。
#
# Unity 2021.2 自己的产物是：
#     format 7 | blocksInfo 内联 | 头部补零对齐到 16 字节 | flags = 0x43(LZ4HC)
# 这里按同样的布局手写容器，内部的 SerializedFile 字节原样搬运（格式版本同为 22）。
# --------------------------------------------------------------------------
def write_unityfs(files, out_path, unity_version="2021.2.10f1",
                  block_size=0x20000, comp_flag=3, bundle_format=7):
    """files: {name: File}；写出 Unity 2021.2 可加载的 UnityFS 包"""
    import lz4.block
    from UnityPy.streams import EndianBinaryReader, EndianBinaryWriter

    payload = bytearray()
    dirs = []
    for name, f in files.items():
        if isinstance(f, (EndianBinaryReader, EndianBinaryWriter)):
            data = bytes(f.bytes)
        else:
            data = bytes(f.save())
        dirs.append((name, getattr(f, "flags", 4), len(payload), len(data)))
        payload += data

    # 分块压缩
    blocks, blob = [], bytearray()
    for off in range(0, len(payload), block_size):
        chunk = bytes(payload[off:off + block_size])
        c = lz4.block.compress(chunk, mode="high_compression", store_size=False)
        blocks.append((len(chunk), len(c), comp_flag))
        blob += c

    # blocksInfo：16 字节 hash + 块表 + 文件表
    info = bytearray(b"\x00" * 16)
    info += struct.pack(">i", len(blocks))
    for u, c, fl in blocks:
        info += struct.pack(">IIH", u, c, fl)
    info += struct.pack(">i", len(dirs))
    for name, fl, off, size in dirs:
        info += struct.pack(">qqI", off, size, fl)
        info += name.encode("utf-8") + b"\x00"
    info = bytes(info)
    cinfo = lz4.block.compress(info, mode="high_compression", store_size=False)

    flags = 0x40 | comp_flag          # BlocksAndDirectoryInfoCombined + LZ4HC
    base = b"UnityFS\x00" + struct.pack(">I", bundle_format) + b"5.x.x\x00" + unity_version.encode() + b"\x00"
    hdr_len = len(base) + 20
    pad = (16 - hdr_len % 16) % 16
    total = hdr_len + pad + len(cinfo) + len(blob)
    header = base + struct.pack(">qIII", total, len(cinfo), len(info), flags) + b"\x00" * pad

    os.makedirs(os.path.dirname(out_path), exist_ok=True)
    tmp = out_path + ".tmp"
    with open(tmp, "wb") as fh:
        fh.write(header)
        fh.write(cinfo)
        fh.write(bytes(blob))
    os.replace(tmp, out_path)
    return total

def repack(src, dst, verify=True, expect_cabs=None):
    import UnityPy
    patch_unitypy(True)
    env = UnityPy.load(src)
    # 用 2021.2 兼容布局重写容器（UnityPy 默认写 format 8，2021.2 读不了）
    written = write_unityfs(env.file.files, dst, unity_version=UNITY_TARGET)
    if verify:
        patch_unitypy(False)                                        # 还原原生解压器
        try:
            env2 = UnityPy.load(dst)
            n = len(list(env2.objects))
            if n == 0:
                raise RuntimeError("重打包后 0 个对象")
            if expect_cabs:
                have = set(env2.file.files.keys())
                lost = [c for c in expect_cabs if c not in have]
                if lost:
                    raise RuntimeError(f"重打包后丢失 CAB: {lost}")
        finally:
            patch_unitypy(True)
    return written


# --------------------------------------------------------------------------

def collect_emitter_refs(path):
    """扫一个包里所有 MonoBehaviour，收集发射器引用的子特效名。
    不解析脚本类名（省去 MonoScript 依赖），直接按字段名识别：
    任何带 _effects(数组) 或 _effect(单个) 字符串字段的组件都按发射器处理。"""
    import UnityPy
    refs = []
    try:
        patch_unitypy(True)
        env = UnityPy.load(path)
        for o in env.objects:
            if o.type.name != "MonoBehaviour":
                continue
            try:
                t = o.read_typetree()
            except Exception:
                continue
            v = t.get("_effects")
            if isinstance(v, list):
                refs += [x for x in v if isinstance(x, str) and x]
            v1 = t.get("_effect")
            if isinstance(v1, str) and v1:
                refs.append(v1)
    except Exception:
        pass
    return refs


def main():
    default_src = r"D:\UnityWork\解包\Windows\battle\prefabs\effects"
    ap = argparse.ArgumentParser()
    ap.add_argument("--src", default=default_src)
    ap.add_argument("--project", default=os.path.abspath(os.path.join(os.path.dirname(__file__), "..")))
    ap.add_argument("--dep-root", default=None, help="依赖搜索根目录，默认取 src 往上 3 层（即 Windows/）")
    ap.add_argument("--out", default=None, help="默认 <project>/Assets/StreamingAssets/arkfx")
    ap.add_argument("--series", default=None, help="主包归档子目录名，默认取 src 目录名（effects）")
    ap.add_argument("--all", action="store_true", help="全量打包（默认只打表里用到的）")
    ap.add_argument("--skip-deps", action="store_true")
    ap.add_argument("--limit", type=int, default=0)
    ap.add_argument("--dry-run", action="store_true")
    ap.add_argument("--force", action="store_true", help="已存在的输出也重打")
    ap.add_argument("--no-emitter-expand", action="store_true",
                    help="不递归纳入发射器引用的子特效（默认会纳入）")
    ap.add_argument("--keep-scripts", action="store_true",
                    help="保留纯 MonoScript 依赖包（默认跳过，ArkFx 用替身不需要）")
    args = ap.parse_args()

    t0 = time.time()
    src = os.path.abspath(args.src)
    project = os.path.abspath(args.project)
    out = os.path.abspath(args.out or os.path.join(project, "Assets", "StreamingAssets", "arkfx"))
    series = args.series or os.path.basename(src.rstrip("\\/"))
    dep_root = os.path.abspath(args.dep_root or os.path.join(src, "..", "..", ".."))

    print(f"源目录   : {src}")
    print(f"依赖根   : {dep_root}")
    print(f"输出目录 : {out}")
    print(f"归档系列 : {series}")
    print("-" * 78)

    # ---- 1. 扫描源包 ----
    files = sorted(os.path.join(src, f) for f in os.listdir(src) if f.lower().endswith(".ab"))
    if args.limit:
        files = files[:args.limit]
    print(f"[1/5] 扫描 {len(files)} 个源包 ...")
    meta = {}
    cache = load_probe_cache(project)
    for i, p in enumerate(files, 1):
        meta[p] = probe(p, cache)
        meta[p]["bytes"] = os.path.getsize(p)
        if i % 50 == 0:
            print(f"      {i}/{len(files)}  {time.time()-t0:.0f}s", flush=True)

    bad = [p for p, m in meta.items() if "error" in m]
    if bad:
        print(f"      [!] {len(bad)} 个包解析失败，已跳过：{[os.path.basename(b) for b in bad[:5]]}")

    # ---- 2. 选择要打包的包 ----
    wanted = load_wanted(project)
    if args.all:
        chosen = [p for p in files if "error" not in meta[p]]
        print(f"[2/5] 全量模式：{len(chosen)} 个包")
    else:
        all_prefabs = {n for m in meta.values() for n in m.get("prefabs", ())}
        hit = {n for n in wanted if n in all_prefabs}
        chosen = [p for p in files
                  if "error" not in meta[p] and set(meta[p]["prefabs"]) & set(wanted)]
        miss = sorted(set(wanted) - all_prefabs)
        print(f"[2/5] 表内 {len(wanted)} 个特效名，源包内命中 {len(hit)}，缺失 {len(miss)}")
        print(f"      选中 {len(chosen)} 个包 / {sum(meta[p]['bytes'] for p in chosen)/1e6:.1f} MB")
        if miss:
            print(f"      缺失（多为工程自建名，忽略即可）: {miss[:10]}")

    # ---- 2.5 发射器子特效递归展开 ----
    # 发射器（OnPlayEmitter 等）会按名字生成子特效，这些子特效可能不在表里，
    # 若没被选中就会在运行时「找不到 prefab」。这里做一次闭包展开。
    if not args.no_emitter_expand:
        name2path = {}
        for _p, _m in meta.items():
            for _n in _m.get("prefabs", ()):
                name2path.setdefault(_n, _p)
        chosen_set = set(chosen)
        frontier = list(chosen)
        added = []
        while frontier:
            nxt = []
            for path in frontier:
                for ref in collect_emitter_refs(path):
                    tgt = name2path.get(ref)
                    if tgt and tgt not in chosen_set:
                        chosen_set.add(tgt)
                        nxt.append(tgt)
                        added.append(ref)
            frontier = nxt
        if added:
            print(f"      发射器闭包：额外纳入 {len(added)} 个子特效，新增 {len(chosen_set) - len(chosen)} 个包")
        else:
            print("      发射器闭包：无需额外纳入（引用的子特效都已在选中包里）")
        chosen = sorted(chosen_set, key=lambda p: os.path.basename(p))

    # ---- 3. 依赖传递闭包 ----
    deps_needed = set()
    for p in chosen:
        deps_needed.update(meta[p].get("deps", []))
    print(f"[3/5] 直接依赖 {len(deps_needed)} 个 CAB，解析提供者并做传递闭包 ...")
    chosen_set = {os.path.normcase(p) for p in chosen}
    dep_files = {}            # cab -> provider path
    resolved = set()
    UNRESOLVED_VERDICT = {}
    if not args.skip_deps:
        idx, scanned = index_cab_providers(dep_root)
        print(f"      建立 CAB 索引：{scanned} 个候选包，{len(idx)} 个 CAB")
        while True:
            todo = deps_needed - resolved
            if not todo:
                break
            progressed = False
            for cab in list(todo):
                cands = [c for c in idx.get(cab, []) if os.path.normcase(c) not in chosen_set]
                if not cands:
                    resolved.add(cab)          # 找不到就记一笔，避免死循环
                    continue
                prov = pick_provider(cands, dep_root)
                dep_files[cab] = prov
                resolved.add(cab)
                progressed = True
                m = meta.get(prov) or probe(prov, cache)
                meta[prov] = m
                m.setdefault("bytes", os.path.getsize(prov))
                deps_needed.update(m.get("deps", []))
            if not progressed:
                break
        unresolved = sorted(c for c in deps_needed if c not in dep_files)
        if unresolved:
            print(f"      [!] {len(unresolved)} 个依赖在整个解包树里找不到提供者，正在判断影响 ...")
            usage = {}
            for sample in chosen[:3]:
                for cab, types in classify_deps(sample).items():
                    if cab in unresolved:
                        acc = usage.setdefault(cab, collections.Counter())
                        acc.update(types)
            for cab in unresolved:
                u = usage.get(cab)
                if not u:
                    verdict = "无法判断"
                elif set(u) <= {"MonoBehaviour"}:
                    verdict = "无害（原版脚本的 MonoScript，ArkFx 本来就用替身）"
                else:
                    verdict = "有影响：" + ", ".join(f"{k}x{v}" for k, v in u.most_common(4))
                print(f"        {cab}  ->  {verdict}")
                UNRESOLVED_VERDICT[cab] = verdict
    if dep_files and not args.keep_scripts:
        dropped = []
        for cab, p in list(dep_files.items()):
            try:
                names = bundle_object_types(p)
            except Exception:                                   # noqa: BLE001
                continue
            if names and set(names) <= {"MonoScript", "AssetBundle"}:
                dropped.append((cab, p))
                dep_files.pop(cab)
        if dropped:
            print(f"      跳过 {len(dropped)} 个纯 MonoScript 依赖包"
                  f"（原版脚本元数据，ArkFx 用 ArkFxScriptStub 替身）")
            for cab, p in dropped:
                print(f"        {cab[4:12]}  {os.path.relpath(p, dep_root)}")

    print(f"      依赖包 {len(dep_files)} 个 / "
          f"{sum(meta[p]['bytes'] for p in dep_files.values())/1e6:.1f} MB")
    for cab, p in sorted(dep_files.items(), key=lambda kv: -meta[kv[1]]['bytes']):
        print(f"        {meta[p]['bytes']/1e6:8.2f} MB  {cab[4:12]}  {os.path.relpath(p, dep_root)}")

    # ---- 4. 重打包 ----
    # 同一个源 .ab 可能同时提供多个 CAB（内含多个 SerializedFile），只能打一份，
    # 否则运行时会用两个不同文件名加载同一份内容 → CAB 名冲突。
    dep_by_file = collections.OrderedDict()
    for cab, p in sorted(dep_files.items()):
        dep_by_file.setdefault(os.path.normcase(p), (p, []))[1].append(cab)
    jobs = []
    for p in chosen:
        stem = os.path.splitext(os.path.basename(p))[0]
        jobs.append((p, os.path.join(out, series, f"{stem}_lz4.ab"), None))
    for _k, (p, cabs) in dep_by_file.items():
        jobs.append((p, os.path.join(out, "_deps", f"{cabs[0][4:12]}_lz4.ab"), cabs))

    print(f"[4/5] 重打包 {len(jobs)} 个包 ...")
    results, failed = [], []
    if not args.dry_run:
        for i, (s, d, cabs) in enumerate(jobs, 1):
            try:
                if not args.force and os.path.isfile(d):
                    results.append({"src": s, "dst": d, "bytes": os.path.getsize(d), "cached": True})
                else:
                    n = repack(s, d, expect_cabs=cabs)
                    results.append({"src": s, "dst": d, "bytes": n, "cached": False})
            except Exception as e:                                  # noqa: BLE001
                failed.append((s, f"{type(e).__name__}: {e}"))
            if i % 20 == 0 or i == len(jobs):
                print(f"      {i}/{len(jobs)}  {time.time()-t0:.0f}s", flush=True)
    if failed:
        print(f"      [!] {len(failed)} 个包重打包失败：{failed[:3]}")

    save_probe_cache(project, cache)

    # ---- 5. 清单 ----
    print("[5/5] 写 manifest / prefab 清单 ...")
    prefab_index = {}
    for p in chosen:
        stem = os.path.splitext(os.path.basename(p))[0]
        rel = f"{series}/{stem}_lz4.ab"
        for n in meta[p].get("prefabs", []):
            prefab_index.setdefault(n, rel)
    manifest = {
        "generated": time.strftime("%Y-%m-%d %H:%M:%S"),
        "source": src,
        "series": series,
        "mode": "all" if args.all else "used-only",
        "bundles": [{
            "file": f"{series}/{os.path.splitext(os.path.basename(p))[0]}_lz4.ab",
            "source": os.path.relpath(p, src),
            "prefabs": meta[p].get("prefabs", []),
            "deps": meta[p].get("deps", []),
        } for p in chosen],
        "deps": [{"cabs": cabs,
                  "file": f"_deps/{cabs[0][4:12]}_lz4.ab",
                  "source": os.path.relpath(p, dep_root)}
                 for p, cabs in dep_by_file.values()],
        "prefab_index": prefab_index,
        "unresolved_deps": UNRESOLVED_VERDICT,
    }
    if not args.dry_run:
        os.makedirs(out, exist_ok=True)
        with open(os.path.join(out, "_manifest.json"), "w", encoding="utf-8") as fh:
            json.dump(manifest, fh, ensure_ascii=False, indent=1)
        md = os.path.join(out, "prefab清单.md")
        with open(md, "w", encoding="utf-8") as fh:
            fh.write(f"# ArkFx prefab 清单（{series}）\n\n")
            fh.write(f"> 由 `Tools/arkfx_merge.py` 自动生成于 {manifest['generated']}\n")
            fh.write(f"> 共 {len(prefab_index)} 个 prefab，来自 {len(chosen)} 个包。"
                     f"填 `ArkFxSlot.prefabName` 时直接用下表左列。\n\n")
            fh.write("| prefab 名 | 所在包（abFile） |\n|---|---|\n")
            for n in sorted(prefab_index):
                fh.write(f"| `{n}` | `{os.path.basename(prefab_index[n])}` |\n")
        print(f"      {os.path.join(out, '_manifest.json')}")
        print(f"      {md}")

    if args.dry_run:
        plan = sum(os.path.getsize(s) for s, _d, _c in jobs)
        print("-" * 78)
        print(f"dry-run：计划重打包 {len(jobs)} 个包（源体积 {plan/1e6:.1f} MB），未写盘，用时 {time.time()-t0:.0f}s")
        return 0
    total = sum(r["bytes"] for r in results)
    print("-" * 78)
    print(f"完成：{len(results)} 个包，{total/1e6:.1f} MB，用时 {time.time()-t0:.0f}s")
    return 0 if not failed else 1


if __name__ == "__main__":
    sys.exit(main())
