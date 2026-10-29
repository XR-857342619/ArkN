#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
verify_effect_p0.py —— 特效 P0 时序/生命周期修复的静态回归检查

不启动 Unity，也不替代运行时人工验收；它只确认关键修复没有被后续改动误删：
  1. EffectData.LifeTime 仍有默认值，且 Excel 导出生成器会保留它
  2. ArkFx 的 Kick 不再发生在 GetEffect / BulletManager.Get（避免零位置启动）
  3. Effect.Play() 才首次 Kick，且 Init() 在定位后才 Play
  4. ArkFx 宿主存在时 Play() 不会强行 SetActive(true)（避免抵消 FxDelay）
  5. OnFinishEmitter 不再用 clearWhenFinish 清掉自己刚发射的尾段特效
  6. 同一个 AB prefab 被多条 EffectData 引用时不会重复 AddComponent<Effect>

用法：
    python Tools/verify_effect_p0.py
退出码：0=全部通过；1=有检查项失败
"""
import os
import sys

# Windows 控制台默认 GBK，直接 print 中文/符号可能 UnicodeEncodeError
try:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
except Exception:
    pass

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
FAILS = []


def read(rel):
    p = os.path.join(ROOT, rel)
    if not os.path.isfile(p):
        FAILS.append(f"文件不存在: {rel}")
        return ""
    with open(p, encoding="utf-8", errors="ignore") as fh:
        return fh.read()


def check(name, cond, detail=""):
    print(("PASS  " if cond else "FAIL  ") + name + (f"  [{detail}]" if detail else ""))
    if not cond:
        FAILS.append(name)


def main():
    effect_data = read("Assets/Scripts/Config/EffectData.cs")
    excel_export = read("Assets/Editor/ExcelEditor/ExcelExportEditor.cs")
    effect = read("Assets/Scripts/Core/Effect/Effect.cs")
    effect_manager = read("Assets/Scripts/VFX/EffectManager.cs")
    bullet_manager = read("Assets/Scripts/Core/Bullet/BulletManager.cs")
    on_finish = read("Assets/Scripts/Core/Effect/ArkFxOnFinishEmitter.cs")
    loader = read("Assets/Scripts/Core/Effect/ArkFxLoader.cs")

    check("EffectData.LifeTime 默认值 = 5f", "public float LifeTime = 5f;" in effect_data)
    check("Excel 导出生成器保留 EffectData.LifeTime 默认值",
          '"EffectData.LifeTime", "5f"' in excel_export)
    check("EffectManager.GetEffect 不再启动 ArkFx（零位置 Kick）",
          "ArkFxLoader.Kick(" not in effect_manager)
    check("BulletManager.Get 不再启动 ArkFx（零位置 Kick）",
          "ArkFxLoader.Kick(" not in bullet_manager)
    check("Effect.Play() 首次启动 ArkFx",
          "ArkFxLoader.Kick(gameObject, PrefabName);" in effect
          and "if (!_started)" in effect)
    check("Effect.Init(Unit...) 在位置/朝向设置后再 Play",
          "transform.eulerAngles = new Vector3(angleX, angleY, angleZ);" in effect
          and effect.find("// 位置/父节点/缩放/朝向全部确定后再启动") > 0)
    check("Effect.Init(Bullet...) 先定位再 Play",
          "transform.position = target.Position;" in effect
          and "// 先定位再启动" in effect)
    check("宿主存在时 Play() 不强行 SetActive(true)",
          "if (_arkFxHost != null)" in effect
          and "// FxDelay 可能刚刚隐藏了根节点；不要在这里 SetActive(true) 抵消延迟。" in effect)
    check("粒子回收包含 inactive 子节点",
          "GetComponentsInChildren<ParticleSystem>(true)" in effect)
    check("OnFinishEmitter 只发射、不清理自己刚发射的尾段",
          "public override void OnFinish()" in on_finish
          and "OnFinishEmit();" in on_finish
          and "ClearSpawned" not in on_finish)
    check("ArkFxLoader 不再给 OnFinishEmitter 开 clearWhenFinish",
          "em.clearWhenFinish = false;" in loader)
    check("EffectManager 不再重复 AddComponent<Effect>()",
          "go.GetComponent<Effect>() == null" in effect_manager)

    print("-" * 72)
    if FAILS:
        print("判定: [FAIL] " + str(len(FAILS)) + " 项失败")
        for x in FAILS:
            print("  - " + x)
        return 1
    print("判定: [OK] 全部通过（仍需按文档做一次运行时人工验收）")
    return 0


if __name__ == "__main__":
    sys.exit(main())
