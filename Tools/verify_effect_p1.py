#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
verify_effect_p1.py —— ArkFx P1（池化复位 + 参数管线）静态/数据检查

检查内容：
  1. ArkFxUVTweenV6 / ArkFxUVTweenAdvanceV6 有初始材质快照与 OnRecycle 复位
  2. ArkFxScriptStub 支持多条目，ArkFxLoader 通过 stub.Add 存档 raw
  3. dump_fx_params.py 已包含 RAW_CLASSES 与 raw 字段
  4. fxparams.json 已导出 Switchable* / PauseEffectIf* / ColorModifier / FxLODSetting 等
  5. 每条 raw 都是合法 JSON

不启动 Unity。运行时验收仍见 Docs/开发者文档/27-ArkFx-P1池化与参数管线.md。

用法：
    python Tools/verify_effect_p1.py
退出码：0=全部通过；1=有失败项
"""
import json
import os
import sys

try:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
except Exception:
    pass

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
FAILS = []
RAW_REQUIRED = (
    "SwitchableBackEffect",
    "SwitchableRightEffect",
    "SwitchableEffectByFaceDirection",
    "PauseEffectIf",
    "ColorModifier",
    "FxLODSetting",
    "ProjectileEffect",
    "FollowDirection",
)


def read(rel):
    p = os.path.join(ROOT, rel)
    if not os.path.isfile(p):
        FAILS.append("文件不存在: " + rel)
        return ""
    with open(p, encoding="utf-8", errors="ignore") as fh:
        return fh.read()


def check(name, cond, detail=""):
    print(("PASS  " if cond else "FAIL  ") + name + (f"  [{detail}]" if detail else ""))
    if not cond:
        FAILS.append(name)


def check_uv(rel):
    s = read(rel)
    return ("_initialMainOffset" in s and "_capturedInitial" in s
            and "public override void OnRecycle()" in s
            and "_initialSecondST" in s)


def main():
    v6 = read("Assets/Scripts/Core/Effect/ArkFxUVTweenV6.cs")
    adv = read("Assets/Scripts/Core/Effect/ArkFxUVTweenAdvanceV6.cs")
    stub = read("Assets/Scripts/Core/Effect/ArkFxScriptStub.cs")
    loader = read("Assets/Scripts/Core/Effect/ArkFxLoader.cs")
    dumper = read("Tools/dump_fx_params.py")

    check("ArkFxUVTweenV6 有初始快照 + OnRecycle 复位", check_uv("Assets/Scripts/Core/Effect/ArkFxUVTweenV6.cs"))
    check("ArkFxUVTweenAdvanceV6 有初始快照 + OnRecycle 复位", check_uv("Assets/Scripts/Core/Effect/ArkFxUVTweenAdvanceV6.cs"))
    check("ArkFxUVTweenAdvanceV6 保存 extraMaps 初始 ST", "_initialExtraST" in adv)
    check("ArkFxScriptStub 支持 entries 多条目", "List<Entry> entries" in stub and "public void Add(" in stub)
    check("ArkFxLoader 使用 stub.Add 存档 raw", "stub.Add(e.cls, e.raw);" in loader)
    check("ArkFxLoader 用 baked 标记而非覆盖 className", "marker.baked = true;" in loader)
    check("dump 脚本包含 RAW_CLASSES", "RAW_CLASSES" in dumper and "raw" in dumper)

    for rel in ("Assets/StreamingAssets/arkfx/fxparams.json",
                "Assets/StreamingAssets/arkfx_android/fxparams.json"):
        path = os.path.join(ROOT, rel)
        if not os.path.isfile(path):
            check("fxparams 存在: " + rel, False)
            continue
        try:
            d = json.load(open(path, encoding="utf-8"))
        except Exception as e:
            check("fxparams 可解析: " + rel, False, str(e))
            continue

        cls = {}
        raw_ok = raw_bad = 0
        for p in d.get("prefabs", []):
            for e in p.get("entries", []):
                cls[e.get("cls")] = cls.get(e.get("cls"), 0) + 1
                raw = e.get("raw")
                if raw is not None:
                    try:
                        json.loads(raw)
                        raw_ok += 1
                    except Exception:
                        raw_bad += 1

        missing = [c for c in RAW_REQUIRED if c not in cls]
        check("fxparams 含目标 raw 类: " + rel, not missing, "missing=" + ",".join(missing) if missing else f"raw_ok={raw_ok}")
        check("fxparams raw 全部是合法 JSON: " + rel, raw_bad == 0, f"raw_ok={raw_ok}, raw_bad={raw_bad}")

    print("-" * 72)
    if FAILS:
        print("判定: [FAIL] " + str(len(FAILS)) + " 项失败")
        for x in FAILS:
            print("  - " + x)
        return 1
    print("判定: [OK] P1 静态/数据检查全部通过（仍需按文档做运行时人工验收）")
    return 0


if __name__ == "__main__":
    sys.exit(main())
