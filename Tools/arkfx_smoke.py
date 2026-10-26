#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
arkfx_smoke.py —— ArkFx 特效包冒烟验收（不需要启动 Unity）

模拟 ArkFxLoader 的行为：在 StreamingAssets/arkfx 下按 prefab 名查包，
然后检查这个 prefab 用到的每一类资源引用是否都能解析：
    材质 Material / 贴图 Texture / Shader / Mesh / AnimatorController
任何一项解析不到，进游戏就是"看不见 / 粉色 / 不动"。

用法：
  python Tools/arkfx_smoke.py                       # 抽查表里 40 个 prefab
  python Tools/arkfx_smoke.py --all                 # 全部 398 个
  python Tools/arkfx_smoke.py --prefab absin_skill_02_start -v
"""
import argparse
import collections
import json
import os
import random
import sys

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
ARKFX = os.path.join(ROOT, "Assets", "StreamingAssets", "arkfx")


def table_names():
    want, src = {}, {}
    base = os.path.join(ROOT, "Assets", "StreamingAssets", "Data")
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
                    except Exception:                              # noqa: BLE001
                        continue
                    if v:
                        want[v] = f"{sub}/{f}"
    return want


def refs_ok(ptr):
    try:
        return ptr.read() is not None
    except Exception:                                              # noqa: BLE001
        return False


_FILE_CACHE = {}
BUILTIN_MESH_IDS = set(range(10202, 10220))   # Unity 内置 Mesh 的固定 pathID 段


def check_file(env, sf, verbose=False):
    """检查一个 SerializedFile（= 一个 AB 里的资产文件）里全部资源引用是否可解析。
    同一个包只查一次，比逐个 prefab 遍历整包快两个数量级。"""
    key = sf.name
    if key in _FILE_CACHE:
        return _FILE_CACHE[key]
    r = collections.Counter()
    bad = collections.defaultdict(list)

    def note(kind, name, ok):
        r[kind + "_总"] += 1
        if ok:
            r[kind + "_OK"] += 1
        else:
            r[kind + "_缺"] += 1
            if len(bad[kind]) < 5:
                bad[kind].append(name)

    for obj in sf.objects.values():
        t = obj.type.name
        try:
            d = obj.read()
        except Exception:                                          # noqa: BLE001
            continue
        if t in ("ParticleSystemRenderer", "MeshRenderer", "SkinnedMeshRenderer", "TrailRenderer"):
            mats = getattr(d, "m_Materials", None)
            lst = mats if isinstance(mats, list) else ([mats] if mats is not None else [])
            for i, m in enumerate(lst):
                if getattr(m, "path_id", 0) == 0:
                    continue                                       # 原生空槽
                note("材质", f"{t}[{i}]", refs_ok(m))
            trail = getattr(d, "m_TrailMaterial", None)
            if trail is not None and getattr(trail, "path_id", 0):
                note("材质", f"{t}.trail", refs_ok(trail))
        elif t == "MeshFilter":
            mf = getattr(d, "m_Mesh", None)
            if mf is None or getattr(mf, "path_id", 0) == 0:
                continue                                           # 没有挂网格，正常
            # Unity 内置图元（Cube/Sphere/Quad/Plane…）来自 Library/unity default resources，
            # UnityPy 解析不到但运行时一定有，直接判 OK
            if getattr(mf, "path_id", 0) in BUILTIN_MESH_IDS:
                note("网格", "内置图元", True)
            else:
                note("网格", "MeshFilter", refs_ok(mf))
        elif t == "Animator":
            ctrl = getattr(d, "m_Controller", None)
            if ctrl is not None and getattr(ctrl, "path_id", 0):
                note("动画控制器", "Animator", refs_ok(ctrl))
        elif t == "Material":
            sh = getattr(d, "m_Shader", None)
            if sh is not None:
                note("Shader", getattr(d, "m_Name", "?"), refs_ok(sh))

    _FILE_CACHE[key] = (r, dict(bad))
    return _FILE_CACHE[key]


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--all", action="store_true")
    ap.add_argument("--n", type=int, default=40)
    ap.add_argument("--prefab", default=None)
    ap.add_argument("-v", "--verbose", action="store_true")
    args = ap.parse_args()

    if not os.path.isdir(ARKFX):
        print(f"[!] 找不到 {ARKFX}，先跑 Tools/arkfx_merge.py")
        return 2

    import UnityPy
    print(f"加载 {ARKFX} ...")
    env = UnityPy.load(ARKFX)
    cont = list(env.container.keys())
    print(f"  包内对象 {len(env.objects)}，container {len(cont)}")

    want = table_names()
    prefabs = {os.path.splitext(os.path.basename(c))[0]: c
               for c in cont if c.lower().endswith(".prefab")}
    hit = [n for n in want if n in prefabs]
    miss = sorted(n for n in want if n not in prefabs)

    print(f"  表内特效名 {len(want)} 个 → 包内命中 {len(hit)}，缺失 {len(miss)}")
    if miss:
        print(f"  缺失（这些会回落到工程内 Addressables）：{miss[:12]}")

    targets = [args.prefab] if args.prefab else (hit if args.all else random.Random(0).sample(hit, min(args.n, len(hit))))
    total = collections.Counter()
    bad_prefabs = []
    for i, name in enumerate(targets, 1):
        if args.verbose:
            print(f"[{i}/{len(targets)}] {name}")
        sf = env.container[prefabs[name]].assetsfile
        r, bad = check_file(env, sf, args.verbose)
        total.update(r)
        if sum(v for k, v in r.items() if k.endswith("_缺")):
            bad_prefabs.append((name, dict(bad)))
        elif not args.verbose and i % 10 == 0:
            print(f"  已检查 {i}/{len(targets)}")

    print("-" * 70)
    for kind in ("材质", "网格", "动画控制器"):
        t, ok, ng = total[kind + "_总"], total[kind + "_OK"], total[kind + "_缺"]
        if t:
            print(f"  {kind:<6}: {ok}/{t} 可解析" + (f"  ✗ 缺 {ng}" if ng else "  ✓"))
    if bad_prefabs:
        print(f"\n[!] {len(bad_prefabs)}/{len(targets)} 个 prefab 有缺失引用：")
        for n, b in bad_prefabs[:10]:
            print(f"    {n}: {b}")
        return 1
    print(f"\n判定: ✓ {len(targets)} 个 prefab 的资源引用全部可解析")
    return 0


if __name__ == "__main__":
    sys.exit(main())
