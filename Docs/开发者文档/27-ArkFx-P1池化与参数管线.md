# 27 · ArkFx P1：池化复位与参数管线

> 编写时间：2026-10-28
> 适用工程：`zhou-master`（Unity 2021.3.39f1）
> 关联文档：[24-ArkFx系统现状与能力边界](24-ArkFx系统现状与能力边界.md)、[26-ArkFx-P0回归修复与验收](26-ArkFx-P0回归修复与验收.md)、[22-ArkFx-P0P1验收手册](22-ArkFx-P0P1验收手册.md)

---

## 0. 本轮 P1 完成项

| # | P1 待办 | 状态 | 结果 |
|---|---|---|---|
| P1-1 | UV 替身组件在对象池复用时复位 offset / ST | ✅ 完成 | `ArkFxUVTweenV6` / `ArkFxUVTweenAdvanceV6` 增加初始材质快照 + `OnRecycle()` |
| P1-2 | 未实现类的原版参数进入 `fxparams.json` | ✅ 完成 | `dump_fx_params.py` 新增 `RAW_CLASSES` + `raw` 字段；Windows / Android 两套数据已重导 |
| P1-3 | 未实现类参数有运行时承载对象 | ✅ 完成 | `ArkFxScriptStub` 支持多条 `entries`；`ArkFxLoader` 将 `raw` 存档到节点 |
| P1-4 | 烘焙标记不占用真实脚本记录 | ✅ 完成 | `ArkFxScriptStub.baked` 独立标记，避免与 `_baked` className 冲突 |

> **边界**：本轮仍然只做“参数管线 + 池化正确性”，**没有实现 Switchable* / PauseEffectIf* / ColorModifier / FxLODSetting 等类的执行逻辑**。
> 这些类现在能被完整保存和检查，后续实现替身时不再需要重新解包 AB。

---

## 1. UV 池化复位（P1-1）

### 1.1 问题

旧实现里，UV 组件在 `OnEnable()` 中读取材质当前 `mainTextureOffset` 作为初值：

```csharp
_mainOffset = keepInitOffset ? _mat.mainTextureOffset : Vector2.zero;
```

对象池复用顺序是：

```text
SetActive(false) → 回池
SetActive(true)  → 再次 OnEnable
```

如果 `keepInitOffset = true`，第二次 `OnEnable` 读到的是**上一轮播放结束时的 offset**，而不是 prefab 初始值，表现为：

- 同一特效每复用一次，UV 起点继续漂移
- 循环 UV 的相位不稳定
- 第二贴图 `<name>_ST` 的 z/w 同样累积漂移
- `FxUVTweenerAdvance.extraMaps[i].st` 也被上一轮修改值污染

### 1.2 修复

`ArkFxUVTweenV6` / `ArkFxUVTweenAdvanceV6` 新增：

```text
实例首次 Awake
  → 用 renderer.material 生成实例材质
  → 采集 _initialMainOffset / _initialSecondST
  → Advance 额外采集 _initialExtraST[]
```

`BindMaterial()` 不再读取“当前材质值”，而是：

```text
keepInitOffset = true  → 使用实例初始快照
keepInitOffset = false → 使用 0 / (1,1,0,0)
```

`OnRecycle()`：

- 清零运行时 `_mainOffset` / `_secondST` / `_time`（Advance）
- 把材质 `mainTextureOffset`、`<name>_ST`、extraMaps 材质属性恢复为初始快照
- 让下一次 `OnEnable` 从同一初始状态开始

宿主回池路径：

```text
EffectManager.ReturnEffect
  → Effect.ResetEffect
      → ArkFxEffectHost.Recycle
          → ArkFxUVTweenV6.OnRecycle
          → ArkFxUVTweenAdvanceV6.OnRecycle
```

---

## 2. 未实现类参数管线（P1-2 / P1-3）

### 2.1 之前的问题

`dump_fx_params.py` 的 `SUPPORTED` 只有 9 类：

```text
FxUVTweener
FxUVTweenerAdvance
FxUVRotation
FxDelay
OnPlayEmitter
OnFinishEmitter
RandomEmitter
OnPlayEmitterIf
FollowMountPoint
```

`Switchable* / PauseEffectIf* / ColorModifier / FxLODSetting` 等类根本不会进入
`fxparams.json`，`ArkFxScriptStub.rawJson` 永远是空的。后续实现替身类时缺少参数来源。

### 2.2 现在的数据格式

新增 `RAW_CLASSES` 集合，命中后写入：

```json
{
  "node": "static_offset/rotation_z",
  "cls": "ColorModifier",
  "raw": "{\"_color\":{\"r\":0.5,\"g\":0.5,\"b\":0.5,\"a\":0.89},\"_pauseIfOwnerDisappear\":1}"
}
```

`raw` 是原版 typetree 的 JSON 字符串（PPtr / 特殊对象统一转字符串，NaN/Infinity 转字符串）。
`ParticleEffect` 被**刻意排除**在 `RAW_CLASSES` 之外：

- 它的 1078 个实例字段多、体积大；
- 其 `DoPlay()` 已被 `ArkFxKick` 的粒子播放覆盖；
- 其生命周期由 `EffectData.LifeTime` + `Effect.cs` 接管。

### 2.3 运行时承载

`ArkFxScriptStub` 现在支持多条记录：

```csharp
public List<Entry> entries;      // 同一节点上的多个原版脚本
public string className;         // 兼容旧字段：第一条
public string rawJson;           // 兼容旧字段：第一条
public bool baked;               // BakePrefab 标记
```

`ArkFxLoader.ApplyFxParams` 的兜底分支：

```csharp
var stub = node.gameObject.GetComponent<ArkFxScriptStub>();
if (stub == null) stub = node.gameObject.AddComponent<ArkFxScriptStub>();
stub.Add(e.cls, e.raw);
```

同一节点有 `SwitchableBackEffect + PauseEffectIf` 等多个类时，不会互相覆盖。

### 2.4 本次重导结果

| 平台 | prefab | 参数条目 | 其中 raw 条目 | fxparams.json 体积 |
|---|---:|---:|---:|---:|
| Windows `arkfx/` | 9410 | 17753 | 3928 | 约 6.5 MB |
| Android `arkfx_android/` | 9461 | 17777 | 3946 | 约 6.5 MB |

重点类的 raw 实例数（Windows）：

| 类 | 实例数 | 作用 |
|---|---:|---|
| `SwitchableBackEffect` | 430 | 背面/朝向切换 |
| `SwitchableRightEffect` | 84 | 左右切换 |
| `SwitchableEffectByFaceDirection` | 23 | 四方向切换 |
| `PauseEffectIf` | 108 | 条件暂停 |
| `PauseEffectIfDisappeared` | 6 | 消失时暂停 |
| `ColorModifier` | 12 | 颜色叠加 |
| `FxLODSetting` | 500 | 低配降级 |
| `ProjectileEffect` | 1112 | 弹道式特效（逻辑由 BulletManager 接管） |
| `FollowDirection` | 1323 | 朝向跟随 |

---

## 3. 代码/工具改动清单

| 文件 | 改动 |
|---|---|
| `Assets/Scripts/Core/Effect/ArkFxUVTweenV6.cs` | 初始 offset/ST 快照；`OnRecycle()` 复位 |
| `Assets/Scripts/Core/Effect/ArkFxUVTweenAdvanceV6.cs` | 初始 offset/ST/extraMaps 快照；`OnRecycle()` 复位 |
| `Assets/Scripts/Core/Effect/ArkFxScriptStub.cs` | 新增 `Entry` 列表 + `Add()` + `baked` 标记 |
| `Assets/Scripts/Core/Effect/ArkFxLoader.cs` | raw 存档到 `stub.Add`；`baked` 标记；加载日志统计 raw 条数 |
| `Assets/Scripts/Core/Effect/ArkFxDiag.cs` | 打印 stub 的 entries 数量与每一条类名/raw 长度 |
| `Tools/dump_fx_params.py` | 新增 `RAW_CLASSES` / `raw` 字段 / NaN-Inf 安全序列化 |
| `Tools/verify_effect_p1.py` | 新增 P1 静态+数据校验脚本 |
| `Assets/StreamingAssets/arkfx/fxparams.json` | Windows 重新生成 |
| `Assets/StreamingAssets/arkfx_android/fxparams.json` | Android 重新生成 |

---

## 4. 自动验证

### 4.1 编译

```bash
dotnet build Assembly-CSharp.csproj -v:minimal -nologo
dotnet build Assembly-CSharp-Editor.csproj -v:minimal -nologo
```

期望：`0 个错误`。

### 4.2 P1 静态/数据检查

```bash
python Tools/verify_effect_p1.py
```

期望最后一行：

```text
判定: [OK] P1 静态/数据检查全部通过（仍需按文档做运行时人工验收）
```

### 4.3 重新导出参数（改了 AB 后必须做）

```bash
# Windows
python Tools/dump_fx_params.py

# Android
python Tools/dump_fx_params.py ^
  --src "D:\UnityWork\解包\effects_Android" ^
  --manifest "Assets\StreamingAssets\arkfx_android\_manifest.json" ^
  --out "Assets\StreamingAssets\arkfx_android\fxparams.json"
```

### 4.4 检查 raw 数据

```bash
python Tools/verify_effect_p1.py
```

或直接看 `fxparams.json` 中是否出现：

```text
SwitchableBackEffect
PauseEffectIf
ColorModifier
FxLODSetting
ProjectileEffect
FollowDirection
```

以及对应条目的 `"raw"` 字段。

---

## 5. 人工手动验收

> 以下步骤在 Unity Editor 的 Play 模式下完成。
> P1 的 UV 复位是“对象池复用正确性”，必须**连续释放同一个特效多次**才能看出来。

### 5.1 验收 A：UV 主贴图 offset 不随池化复用漂移

**代表特效**

| EffectData Id | Prefab | 说明 |
|---|---|---|
| `凯尔希buff3` | `kalts_skill_03_buff` | `keepInitOffset = true`，主贴图 + `_DisturTex` |
| `洁哥2抬手` | `aglina_skill_02_start` | 多 UV 节点，便于观察 |
| `洁哥蓄力` | `aglina_skill_02_gather` | `FxLODSetting` + UV |

**操作**

1. 进入战斗，触发一次上述 Buff/技能。
2. 暂停游戏，在 Hierarchy 选中特效实例。
3. 执行菜单 `Tools/ArkFx/6. 采样选中特效的 UV 偏移（约 2 秒）`，记录主贴图 `_MainTex` 的**首个 offset**。
4. 恢复游戏，等特效回池。
5. 重复触发同一个特效 3 次，每次都记录首个 offset。
6. 对比 3 次的起始 offset。

**期望**

- 3 次的起始 offset 应在同一初始值附近；
- 不会出现“第 2 次从第 1 次结束值继续、第 3 次再继续”的单调漂移；
- 选中效果执行 `Tools/ArkFx/2` 时，`ArkFxUVTweenV6` 段的 `keepInitOffset` / `current _MainTex_ST` 应能对应初始值。

**失败判据**

- 每次重放后 UV 起始 offset 都比上一次更远；
- 第二贴图 `_DisturTex_ST` 的 z/w 也持续漂移；
- 采样结果出现“首值 = 上次末值”。

### 5.2 验收 B：第二贴图 `_ST` 与 Advance extraMaps 复位

**操作**

1. 选 `凯尔希buff3`（`kalts_skill_03_buff`，带 `_DisturTex`）。
2. 触发后暂停，执行 `Tools/ArkFx/2`。
3. 查看 `当前 _DisturTex_ST=...`。
4. 重复触发 3 次，每次都记录刚触发后的 `_DisturTex_ST`。
5. 如果有 `FxUVTweenerAdvance` 的 extraMaps，也查看对应 `<mapName>_ST`。

**期望**

- 每次 `_DisturTex_ST` 的起始 z/w 一致；
- extraMaps 的 `st` 不因复用而累积；
- `FxUVTweenerAdvance` 的 `_time` 在复用后重新从 0 开始（Inspector 不可见时以行为为准：非循环动画应重新播放，而不是直接停住）。

### 5.3 验收 C：未实现类 raw 参数已在运行时存档

> 这只是**存档验收**；这些类本轮不执行，所以暂时不应期待视觉行为变化。

**代表特效**

| 类 | EffectData Id | Prefab | 预期 raw 记录 |
|---|---|---|---|
| `SwitchableBackEffect` | `纯烬艾雅法拉3启动` | `agoat2_skill_03_start_01` | `entries >= 1`，含 `_backEffect` |
| `PauseEffectIf` | `寒冷` | `buff_cold` | `entries >= 1`，含 `_pauseWhenAbnormalFlag` |
| `ColorModifier` | `隐身` | `common_invisible` | `entries >= 1`，含 `_color` |
| `FxLODSetting` | `洁哥2抬手` | `aglina_skill_02_start` | `entries >= 1`，含 `_disableObjects` |
| `ProjectileEffect` | `魔法拖尾1` | `common_magic_trail_01` | `entries >= 1` |

**操作**

1. 触发对应特效。
2. 暂停游戏，Hierarchy 选中特效实例。
3. 执行 `Tools/ArkFx/2. 打印选中对象的 ArkFxDiag`。
4. 找到 `[ArkFx] ArkFxScriptStub` 段。

**期望**

```text
[ArkFx] ArkFxScriptStub baked=True entries=1 原版类名=SwitchableBackEffect
    [0] SwitchableBackEffect raw=... chars
```

- `baked=True`
- `entries` 数量与实际节点上的原版脚本数量一致
- `raw` 不是 `<空>`
- 同一节点同时有多个未实现类时，应看到多条 `[i]` 记录

**失败判据**

- 找不到 `ArkFxScriptStub`；
- `entries=0`；
- `raw=<空>`；
- 同一节点多个类只剩最后一条。

### 5.4 验收 D：连续释放 3 次（回归）

> 沿用 P0 的强制回归用例，P1 改动后必须重跑。

1. 连续释放同一个技能 3 次。
2. 检查：
   - 第 1/2/3 次粒子都播放；
   - 子特效都正常发射；
   - FxDelay 仍然生效；
   - UV 起始相位一致；
   - 没有上一轮残留粒子/拖尾。

---

## 6. 已知限制

1. **本轮只做参数存档，不执行未实现类**：
   - `Switchable*` 仍然不会切换左右/背向；
   - `PauseEffectIf*` 仍然不会按条件暂停；
   - `ColorModifier` 仍然不会染色；
   - `FxLODSetting` 仍然不会低配降级。
   这些需要在后续 P2 逐个实现。
2. `ParticleEffect` 没有写入 raw（见 §2.2）；其行为仍由 `EffectData.LifeTime` + `Effect.cs` 接管。
3. `raw` 是原版 typetree 的 JSON 字符串，不使用强类型反序列化；后续实现替身时应按目标类的 `arkfx_script_classes.json` 字段名读取，并补字段级测试。
4. `fxparams.json` 与 AB 版本强耦合：重新打包 AB 后必须重跑 `dump_fx_params.py`，否则 raw/参数会错位。

---

## 7. 回滚

| 想回滚的部分 | 操作 |
|---|---|
| 只回滚 UV 复位 | 用 git 还原 `ArkFxUVTweenV6.cs` / `ArkFxUVTweenAdvanceV6.cs`；`raw` 管线不受影响 |
| 只回滚参数管线 | 还原 `dump_fx_params.py` / `ArkFxScriptStub.cs` / `ArkFxLoader.cs` 的 stub 分支，再重跑旧版 `dump_fx_params.py` |
| 回滚数据 | 删除或重生成 `Assets/StreamingAssets/arkfx/fxparams.json` / `arkfx_android/fxparams.json` |
