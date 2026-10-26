#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
dump_fx_params.py —— 从方舟 AB 的内嵌 typetree 导出原版脚本参数 → fxparams.json

ArkFxLoader.ApplyFxParams 读这个文件，把原版 prefab 上的
FxUVTweener / FxUVTweenerAdvance / FxUVRotation 参数还原到替身组件上。

数据来源：AB 里 MonoBehaviour 的 serialized typetree（字段名 + 值都是原版精确值），
不依赖任何人工填表，也不需要 IDA。

用法：
  python Tools/dump_fx_params.py                       # 输出到 Assets/StreamingAssets/arkfx/fxparams.json
  python Tools/dump_fx_params.py --out /tmp/fx.json
  python Tools/dump_fx_params.py --all                 # 连未打表的包也 dump（默认只 dump 已打包的）
"""
import argparse
import json
import os
import sys

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))

# 已实现替身的类 -> 字段映射（字段名与原版 serialized 名一致）
SUPPORTED = ("FxUVTweener", "FxUVTweenerAdvance", "FxUVRotation", "FxDelay",
             "OnPlayEmitter", "OnFinishEmitter", "RandomEmitter", "OnPlayEmitterIf",
             "FollowMountPoint")


def patch_unitypy():
    """让 UnityPy 能读方舟 LZ4AK（匿名包/源包都是这种压缩）"""
    try:
        from arkfx_merge import patch_unitypy as _p
        _p(True)
    except Exception:
        pass


def load_script_names(env):
    """MonoBehaviour path_id -> 类名（依赖 anon/ 下的 MonoScript 包）"""
    out = {}
    for o in env.objects:
        if o.type.name != "MonoBehaviour":
            continue
        try:
            c = o.read()
            ms = c.m_Script.read()
            if ms is None:
                continue
            out[o.path_id] = (ms.m_Namespace + "." + ms.m_ClassName) if ms.m_Namespace else ms.m_ClassName
        except Exception:
            pass
    return out


def transform_path(go, root_minus=1):
    """GameObject -> 相对 prefab 根的节点路径（不含根名）"""
    parts = []
    cur = None
    for cp in (go.m_Component or []):
        comp = getattr(cp, "component", cp)
        try:
            cc = comp.read()
        except Exception:
            continue
        if cc is not None and cc.__class__.__name__ == "Transform":
            cur = cc
            break
    depth = 0
    while cur is not None and depth < 64:
        p = cur.m_Father
        if p is None or getattr(p, "path_id", 0) == 0:
            break
        try:
            parent = p.read()
        except Exception:
            break
        if parent is None:
            break
        g = cur.m_GameObject.read()
        if g is not None:
            parts.append(g.m_Name)
        cur = parent
        depth += 1
    parts.reverse()
    return "/".join(parts)


def entries_for_prefab(root_go, scripts):
    """遍历 prefab 子树，收集受支持脚本的参数"""
    out = []
    seen = set()
    stack = [root_go]
    while stack:
        go = stack.pop()
        if go is None or id(go) in seen:
            continue
        seen.add(id(go))
        tr = None
        for cp in (go.m_Component or []):
            comp = getattr(cp, "component", cp)
            try:
                cc = comp.read()
            except Exception:
                continue
            if cc is None:
                continue
            if cc.__class__.__name__ == "Transform":
                tr = cc
            cls = scripts.get(getattr(comp, "path_id", -1))
            if cls is None:
                continue
            short = cls.split(".")[-1]
            if short not in SUPPORTED:
                continue
            try:
                t = comp.read_typetree()
            except Exception:
                continue
            e = {"node": transform_path(go), "cls": short}
            if short in ("FxUVTweener", "FxUVTweenerAdvance"):
                e.update({
                    "keepInitOffset": bool(t.get("keepInitOffset", True)),
                    "useSharedMaterial": bool(t.get("useSharedMaterial", False)),
                    "useSecondMap": bool(t.get("useSecondMap", False)),
                    "secondMapName": t.get("secondMapName") or "",
                    "xspeed": float(t.get("xspeed", 0.0)),
                    "yspeed": float(t.get("yspeed", 0.0)),
                    "secondXSpeed": float(t.get("secondXSpeed", 0.0)),
                    "secondYSpeed": float(t.get("secondYSpeed", 0.0)),
                })
                if short == "FxUVTweener":
                    e["protectMainUV"] = bool(t.get("protectMainUV", False))
                    e["protectSecondUV"] = bool(t.get("protectSecondUV", False))
                else:
                    e["tweenRepeat"] = 1 if t.get("tweenRepeat") else 0
                    e["tweenAnmTime"] = float(t.get("tweenAnmTime", 0.0))
                    extras = []
                    for m in (t.get("extraMapSettings") or []):
                        if not isinstance(m, dict):
                            continue
                        extras.append({
                            "mapName": m.get("mapName") or "",
                            "xSpeed": float(m.get("xSpeed", 0.0)),
                            "ySpeed": float(m.get("ySpeed", 0.0)),
                            "sx": float(m.get("sx", 1.0)),
                            "sy": float(m.get("sy", 1.0)),
                            "ox": float(m.get("ox", 0.0)),
                            "oy": float(m.get("oy", 0.0)),
                        })
                    if extras:
                        e["extras"] = extras
            elif short == "FollowMountPoint":
                e["mountPointType"] = int(t.get("_mountPointType", 0))
                e["followScale"] = float(t.get("_followScale", 0.0))
                e["followScaleSign"] = int(t.get("_followScaleSign", 0))
                e["scaleMultiplier"] = float(t.get("_scaleMultiplier", 1.0))
                e["keepOriginRotation"] = bool(t.get("_keepOriginRotation", False))
                e["pauseWhenMountPointInvalid"] = bool(t.get("_pauseWhenMountPointInvalid", False))
            elif short in ("OnPlayEmitter", "OnFinishEmitter", "OnPlayEmitterIf"):
                e["effects"] = [x for x in (t.get("_effects") or []) if isinstance(x, str)]
                e["instant"] = bool(t.get("_instant", False))
                e["preDelay"] = float(t.get("_preDelay", 0.0))
                e["clearWhenFinish"] = bool(t.get("_clearWhenFinish", True))
                e["useFaceVector"] = bool(t.get("_useFaceVector", False))
                e["useSourceEffectPos"] = bool(t.get("_useSourceEffectPos", False))
                if short == "OnPlayEmitterIf":
                    e["buffKey"] = t.get("_buffKey") or ""
                    e["playWhenFaceToBack"] = bool(t.get("_playWhenFaceToBack", False))
            elif short == "RandomEmitter":
                e["effect"] = t.get("_effect") or ""
                e["count"] = int(t.get("_count", 1))
                e["delayPerEffect"] = float(t.get("_delayPerEffect", 0.0))
                e["preDelay"] = float(t.get("_preDelay", 0.0))
                def _v3(d):
                    d = d or {}
                    return {"x": float(d.get("x", 0.0)), "y": float(d.get("y", 0.0)), "z": float(d.get("z", 0.0))}
                e["randomRangeFrom"] = _v3(t.get("_randomRangeFrom"))
                e["randomRangeTo"] = _v3(t.get("_randomRangeTo"))
            elif short == "FxDelay":
                e["delayTime"] = float(t.get("_delayTime", 0.0))
            else:  # FxUVRotation
                e.update({
                    "rotPropertyName": t.get("_propertyName") or "_Rotation",
                    "rotateTex1": bool(t.get("_rotateTex1", False)),
                    "rotateTex2": bool(t.get("_rotateTex2", False)),
                    "rotateTex3": bool(t.get("_rotateTex3", False)),
                    "rotateTex4": bool(t.get("_rotateTex4", False)),
                    "angle1": float(t.get("angle1", 0.0)),
                    "angle2": float(t.get("_angle2", 0.0)),
                    "angle3": float(t.get("_angle3", 0.0)),
                    "angle4": float(t.get("_angle4", 0.0)),
                })
            out.append(e)
        if tr is not None:
            for ch in (tr.m_Children or []):
                try:
                    child_tr = ch.read()
                    if child_tr is not None:
                        stack.append(child_tr.m_GameObject.read())
                except Exception:
                    pass
    return out


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--src", default=r"D:\UnityWork\解包\Windows\battle\prefabs\effects")
    ap.add_argument("--script-bundle", default=r"D:\UnityWork\解包\Windows\anon\88b3c3ecaaefa6003de730686dd5ac53.bin")
    ap.add_argument("--manifest", default=os.path.join(ROOT, "Assets", "StreamingAssets", "arkfx", "_manifest.json"))
    ap.add_argument("--out", default=os.path.join(ROOT, "Assets", "StreamingAssets", "arkfx", "fxparams.json"))
    ap.add_argument("--all", action="store_true", help="不按 manifest 过滤，dump 源目录全部包")
    args = ap.parse_args()

    import UnityPy
    patch_unitypy()

    src = os.path.abspath(args.src)
    if args.all or not os.path.isfile(args.manifest):
        files = sorted(os.path.join(src, f) for f in os.listdir(src) if f.lower().endswith(".ab"))
    else:
        man = json.load(open(args.manifest, encoding="utf-8"))
        files = [os.path.join(src, b["source"]) for b in man["bundles"]]
    print(f"扫描 {len(files)} 个包 ...")

    # UnityPy 只有按目录加载时才会聚合 container 并解析跨包依赖（MonoScript 在匿名包里），
    # 所以这里建一个硬链接"农场"，把所有输入摆到同一目录再加载。
    import shutil
    import tempfile
    tmp = os.path.join(tempfile.gettempdir(), "arkfx_probe")
    shutil.rmtree(tmp, ignore_errors=True)
    os.makedirs(tmp, exist_ok=True)
    for f in files:
        d = os.path.join(tmp, os.path.basename(f))
        if os.path.exists(d):
            d = os.path.join(tmp, "%04d_%s" % (len(os.listdir(tmp)), os.path.basename(f)))
        try:
            os.link(f, d)
        except Exception:
            shutil.copy2(f, d)
    if os.path.isfile(args.script_bundle):
        shutil.copy2(args.script_bundle, os.path.join(tmp, "zz_scripts.ab"))

    env = UnityPy.load(tmp)
    print("对象数:", len(env.objects), " container:", len(env.container))
    scripts = load_script_names(env)
    print("可识别的 MonoBehaviour 类名:", len(scripts))

    prefabs = []
    total_entries = 0
    for path in [c for c in env.container if c.lower().endswith(".prefab")]:
        try:
            go = env.container[path].read()
        except Exception:
            continue
        entries = entries_for_prefab(go, scripts)
        if not entries:
            continue
        name = os.path.splitext(os.path.basename(path))[0]
        prefabs.append({"prefab": name, "entries": entries})
        total_entries += len(entries)

    doc = {"version": 1, "prefabs": prefabs}
    os.makedirs(os.path.dirname(args.out), exist_ok=True)
    with open(args.out, "w", encoding="utf-8") as f:
        json.dump(doc, f, ensure_ascii=False, indent=1)
    print(f"写出 {args.out}")
    print(f"  prefab {len(prefabs)} 个，参数条目 {total_entries} 条")
    by_cls = {}
    for p in prefabs:
        for e in p["entries"]:
            by_cls[e["cls"]] = by_cls.get(e["cls"], 0) + 1
    print("  按类:", by_cls)
    return 0


if __name__ == "__main__":
    sys.exit(main())
