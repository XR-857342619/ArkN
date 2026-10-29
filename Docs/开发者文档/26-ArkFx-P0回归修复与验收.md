# 26 · ArkFx P0 回归修复与人工验收

> 编写时间：2026-10-28
> 适用工程：`zhou-master`（Unity 2021.3.39f1，版本 v1.7.1_Debug）
> 关联文档：[15-特效系统优化方案](15-特效系统优化方案.md)、[21-ArkFx特效系统改造规划](21-ArkFx特效系统改造规划.md)、[22-ArkFx-P0P1验收手册](22-ArkFx-P0P1验收手册.md)、[24-ArkFx系统现状与能力边界](24-ArkFx系统现状与能力边界.md)

---

## 0. 本次修复解决的问题

这一轮只处理**新旧特效系统对接处的 P0 问题**，不新增 ArkFx 原版脚本替身类。

| # | 问题 | 后果 | 修复文件 |
|---|---|---|---|
| P0-1 | `EffectManager.GetEffect` / `BulletManager.Get` 在零位置、在 `Init()` 之前 `Kick` | `OnPlayEmitter` 在世界原点生成子特效；`FxDelay` 被随后的 `Effect.Play()` 重新激活而失效 | `EffectManager.cs`、`BulletManager.cs`、`Effect.cs` |
| P0-2 | `Effect.Play()` 无脑 `SetActive(true)` | 覆盖 `ArkFxDelay` 刚设置的隐藏状态 | `Effect.cs` |
| P0-3 | `EffectData.LifeTime` 默认值被 Excel 导出生成器覆盖回 0 | 表里未填 `LifeTime` 的一次性特效生成一帧即回池 | `EffectData.cs`、`ExcelExportEditor.cs` |
| P0-4 | `OnFinishEmitter` 在 `OnFinish` 里先发射、再按 `clearWhenFinish` 清空 | 尾段特效同帧被 Destroy | `ArkFxOnFinishEmitter.cs`、`ArkFxLoader.cs` |
| P0-5 | 同一个 AB prefab 被多条 `EffectData` 引用时重复 `AddComponent<Effect>()` | 克隆体多个 `Effect.Update` / 多次 `ReturnEffect` | `EffectManager.cs` |
| P0-6 | `Effect.Awake` 只收集 active 的粒子/拖尾 | 被 `FxDelay` 延迟激活的子粒子回收时漏停，池化复用残留 | `Effect.cs` |

---

## 1. 修复后的正确时序

### 1.1 表驱动特效（Skill / Buff / ReadyEffect）

```text
EffectManager.GetEffect(id)
  → 加载/缓存 prefab
  → Pool.Spawn（此时不 Kick）
  → ResetEffect()（清理上一轮状态）
  → 返回 Effect 实例

调用方：
  → 设置位置/父节点/挂点/缩放/朝向
  → Init(...) 或直接 Play()

Effect.Init(...)
  → 设置 simulationSpeed
  → 计算 Parent / BindPoint / StartPos / Offset / ScaleXFollow / FaceDirection
  → 最后才 Play()

Effect.Play()
  → 首次：ArkFxLoader.Kick(gameObject, PrefabName)
      → ApplyFxParams
      → ArkFxEffectHost.Play()
      → ArkFxDelay 可以安全地隐藏根节点
      → ArkFxOnPlayEmitter 在最终位置发射子特效
  → 非首次：只清拖尾，不重复 Kick
  → ArkFx 来源：绝不在这里 SetActive(true)
  → 工程内来源：沿用旧行为（激活 + 粒子 Play）
```

### 1.2 子弹特效

```text
BulletManager.Get(model)
  → Pool.Spawn（不 Kick）
  → 记录 Effect.PrefabName

Bullet.CreateModel
  → BulletModel.Init(bullet)
      → 先设置 transform.position / rotation
      → GetComponent<Effect>().Play()
          → 首次 Play 才 Kick
```

### 1.3 FxDelay 与 Effect.Play 的边界

`ArkFxDelay` 的语义是“把根节点隐藏，延时后再激活”。因此：

- `Effect.Play()` 不再对 ArkFx 实例强行 `SetActive(true)`；
- `Effect.Update()` 只对**工程内特效**保留旧的自动激活兜底；
- `ArkFxDelay.Reveal()` 负责重新激活并补播隐藏期间未播放的粒子。

---

## 2. 代码改动清单

| 文件 | 改动 |
|---|---|
| `Assets/Scripts/Core/Effect/Effect.cs` | 新增 `PrefabName` / `_arkFxHost` / `_started`；Kick 推迟到首次 `Play()`；`Init()` 在定位后再 Play；宿主存在时 Play 不强行激活；粒子/拖尾收集改为 `includeInactive=true` |
| `Assets/Scripts/VFX/EffectManager.cs` | `GetEffect` 不再 Kick；设置 `result.PrefabName`；`AddComponent<Effect>()` 判重 |
| `Assets/Scripts/Core/Bullet/BulletManager.cs` | `Get` 不再 Kick；只记录 `Effect.PrefabName`，由 `BulletModel.Init → Effect.Play()` 启动 |
| `Assets/Scripts/Core/Skill/Skill.cs` | `ReadyEffect` 不经过 `Init()`，创建并定位后显式 `Play()` |
| `Assets/Scripts/Core/Effect/ArkFxOnFinishEmitter.cs` | 覆盖 `OnFinish()`：只发射，不清理刚发射的尾段特效 |
| `Assets/Scripts/Core/Effect/ArkFxLoader.cs` | `AttachOnFinishEmitter` 固定 `clearWhenFinish = false` |
| `Assets/Scripts/Config/EffectData.cs` | `LifeTime = 5f` |
| `Assets/Editor/ExcelEditor/ExcelExportEditor.cs` | 新增 `ExcelClassFieldInitializers`，生成 `EffectData.LifeTime` 时输出 `= 5f`，避免下次导表覆盖 |
| `Tools/verify_effect_p0.py` | 新增 P0 静态回归检查脚本 |

> ⚠️ `Assets/Scripts/Config/*.cs` 是 `Tools/导出配置` 自动生成的。
> 以后修改默认值必须同时改 `ExcelClassFieldInitializers`，否则下一次导表会丢失。

---

## 3. 编译与静态验证

### 3.1 编译

在仓库根目录执行：

```bash
dotnet build Assembly-CSharp.csproj -v:minimal -nologo
dotnet build Assembly-CSharp-Editor.csproj -v:minimal -nologo
```

期望：

```text
0 个错误
```

> 警告可暂时忽略；本工程由 Unity 生成 csproj，`dotnet build` 主要用于快速发现语法/类型错误。

### 3.2 静态回归检查

```bash
python Tools/verify_effect_p0.py
```

期望最后一行：

```text
判定: [OK] 全部通过（仍需按文档做一次运行时人工验收）
```

该脚本检查 12 个关键不变量，包括：

- `EffectData.LifeTime = 5f` 及导出生成器保留默认值
- `GetEffect / BulletManager.Get` 不再 Kick
- `Effect.Play()` 首次 Kick
- `Init()` 先定位后 Play
- ArkFx 宿主存在时 Play 不强行激活
- OnFinishEmitter 不清空自己刚发射的子特效
- `AddComponent<Effect>()` 判重
- 粒子回收包含 inactive 子节点

---

## 4. 人工手动验收

> 以下步骤必须在 Unity Editor 的 Play 模式中完成。
> 建议先把 Console 清空，并把游戏特效来源切到「原版 AB 优先」。

### 4.0 前置检查

1. 打开工程，等待编译完成，Console 无编译错误。
2. 确认 `Assets/StreamingAssets/arkfx/` 存在，且包含 `effects/`、`_deps/`、`fxparams.json`。
3. 进入 Play，确认 Console 出现：
   ```text
   [ArkFxLoader] 已加载 ... 个包 | ... | fxparams=... 个 prefab
   ```
4. 主界面设置面板确认 `SwitchEffectsSource` 为「原版 AB 优先」。
   - 或使用菜单 `Tools/ArkFx/0. 切换 特效来源优先级 AB优先 <-> 工程内优先`，然后**重新进 Play**。
5. 菜单 `Tools/ArkFx/4. 检查 AB 里的 Shader 是否可用`，确认没有大面积 `FAIL`。

---

### 4.1 验收 A：FxDelay 不再被 Effect.Play 抵消

**目的**：确认 `ArkFxDelay` 根节点隐藏后，`Effect.Init()` 不会把它立刻点亮。

**代表特效**

| EffectData Id | Prefab | FxDelay 时长 |
|---|---|---|
| `42普攻抬手` | `surtr_attack_01_start` | 0.35s |
| `洁哥蓄力` | `aglina_skill_02_gather` | 1.0s |
| `桃金娘抬手` | `myrtle_attack_01_start` | 0.5s |

**操作**

1. 进入战斗，使用带上述特效的单位技能/普攻。
2. 特效触发后**立刻暂停游戏**（战斗界面暂停按钮），或直接点 Hierarchy 中的特效实例。
3. 在 Hierarchy 中找到对应的 `xxx(Clone)` 根节点，选中后执行菜单：
   `Tools/ArkFx/2. 打印选中对象的 ArkFxDiag`。
4. 查看报告里的 `ArkFxDelay` 段和节点状态。

**期望**

- 延迟结束前：
  ```text
  [ArkFx] ArkFxDelay ...
      delayTime=0.35s  waiting=True  activeSelf=False
  ```
  并且该节点及其子粒子不可见。
- 恢复游戏约 0.35s 后：
  - `waiting=False`
  - `activeSelf=True`
  - 粒子开始播放
  - `Surtr` 的抬手特效出现在正确位置。

**失败判据**

- 特效一出现就立即播放（没有延迟），Diag 显示 `waiting=True` 但 `activeSelf=True`；
- 或延迟结束后完全不播放。

---

### 4.2 验收 B：OnPlayEmitter 在最终位置生成子特效

**目的**：确认 `OnPlayEmitter` 不再在世界原点生成子特效。

**代表特效**

| EffectData Id | Prefab | 发射子特效 |
|---|---|---|
| `纯烬艾雅法拉3加成` | `agoat2_skill_03_buff_01` | `agoat2_skill_03_weapon_01..05` |
| `幽灵鲨眼buff` | `ghost_skill_02_buff` | `ghost_skill_02_buff_02` |
| `能天使2buff` | `angle_skill_02_buff` | `angle_skill_02_buff_start` / `_02` |

**操作**

1. 进入战斗，触发对应 Buff/技能（例如纯烬艾雅法拉 3 技能，或幽灵鲨 2 技能）。
2. 特效出现后暂停游戏。
3. 在 Hierarchy 搜索发射出的子特效名，例如 `agoat2_skill_03_weapon_01`。
4. 选中子特效，查看 Inspector 的 `Transform.Position`。

**期望**

- 子特效位于**施法者/目标附近**，而不是世界原点 `(0,0,0)`；
- `agoat2_skill_03_buff_01` 应能看到 5 个 `agoat2_skill_03_weapon_0X` 子对象；
- 选中父特效执行 `Tools/ArkFx/2`，Diag 中：
  ```text
  [ArkFx] ArkFxOnPlayEmitter ...
      已发射子特效=5
  ```

**失败判据**

- 子特效全部堆在世界原点；
- `已发射子特效=0`；
- 子特效位置在池中上一次的位置（连续放两次技能时最明显）。

---

### 4.3 验收 C：未填 LifeTime 的一次性特效不再一帧消失

**目的**：确认 `EffectData.LifeTime` 默认 5 秒生效。

**代表特效**

| EffectData Id | Prefab | 说明 |
|---|---|---|
| `魔法击中` | `common_magic_hit_01` | 表里没有 LifeTime |
| `洁哥蓄力` | `aglina_skill_02_gather` | 表里没有 LifeTime，且带 FxDelay |

**操作**

1. 使用杜林普攻，或安洁莉娜相关技能，触发 `魔法击中` / `洁哥蓄力`。
2. Hierarchy 中搜索 `common_magic_hit_01(Clone)` 或 `aglina_skill_02_gather(Clone)`。
3. 观察对象是否至少存活到粒子自然结束，而不是下一帧消失。

**期望**

- 对象不会在生成后的 1 帧内被回池；
- 粒子能完整播放；
- `ArkFxDiag` 中对象仍存在，`LifeTime` 由 `EffectData` 默认 5 秒兜底。

**失败判据**

- 对象只在 Hierarchy 闪一下；
- 粒子完全看不到；
- Console 无报错但特效“空了”。

---

### 4.4 验收 D：OnFinishEmitter 的尾段特效存活

**目的**：确认父特效结束时发射的尾段不再同帧被 Destroy。

**代表特效**

| EffectData Id | Prefab | OnFinish 发射 |
|---|---|---|
| `羊3buff` | `amgoat_skill_03_buff` | `amgoat_skill_03_buff_end` |
| `华法琳2Buff` | `bldsk_skill_02_buff` | `bldsk_skill_02_buff_end` |

**操作**

1. 进入战斗，触发对应持续 Buff/技能。
2. 等父特效的粒子自然结束（或手动移除 Buff）。
3. 在 Hierarchy 搜索 `amgoat_skill_03_buff_end` 或 `bldsk_skill_02_buff_end`。

**期望**

- 父特效结束的瞬间，尾段特效出现在 Hierarchy 中；
- 尾段特效至少存活数帧并可见，而不是同帧消失；
- 选中父特效执行 `Tools/ArkFx/2` 时，`ArkFxOnFinishEmitter` 段能看到 `effects=...`。

**失败判据**

- 父特效结束后完全找不到尾段；
- 尾段对象只在 Hierarchy 出现一瞬间，粒子从未渲染。

---

### 4.5 验收 E：共享 AB prefab 不重复挂 Effect

**目的**：确认多条 `EffectData` 引用同一个 prefab 时，实例上只有一个 `Effect` 组件。

**代表组合**

以下多个 EffectData Id 共用 `ghost_skill_02_buff`：

- `幽灵鲨眼buff`
- `m3特效buff1`
- `m3特效buff2`
- `黑3眼buff`

**操作**

1. 在同一场战斗内触发至少两个共用 prefab 的效果（例如 M3 的多个 Buff）。
2. 暂停游戏，Hierarchy 选中任意 `ghost_skill_02_buff(Clone)`。
3. 查看 Inspector 顶部的组件列表。

**期望**

- 只有一个 `Effect` 组件；
- 只有一个 `ArkFxEffectHost` 组件；
- 连续触发/对象池复用后，不出现“同一特效多次回池”或 Console 报错。

**失败判据**

- Inspector 中出现多个 `Effect` 组件；
- 特效被回收两次、生命周期异常，或再次播放不显示。

---

### 4.6 验收 F：对象池连续复用（回归用例）

> 该用例是 P3 验收时定下的强制回归项，本轮改动后必须重跑。

**操作**

1. 连续释放同一个技能 3 次（间隔 1~2 秒）。
2. 每次暂停并检查 Hierarchy 中的特效实例。
3. 对第 2、3 次特别检查：
   - 粒子是否正常播放；
   - 子特效是否正常发射；
   - FxDelay 是否仍然生效；
   - 位置/朝向是否正确。

**期望**

- 第 1、2、3 次行为完全一致；
- 不出现“只有第一次正确”；
- 不出现上一轮粒子残留。

---

### 4.7 验收 G：AB 来源 / 工程内来源切换

**操作**

1. 菜单 `Tools/ArkFx/0` 切换特效来源。
2. 重新进 Play。
3. 重复 4.1 / 4.2 中至少一个用例。

**期望**

- 「原版 AB 优先」时走 ArkFx 特效，组件统计中 `ArkFxEffectHost > 0`；
- 「工程内优先」时仍然能正常播放工程内 prefab，不能因为本轮改动而不显示；
- 两种来源都不能出现零位置子特效或 FxDelay 失效。

---

## 5. 已知限制与后续工作

P0 轮当时**没有**处理以下内容；其中第 2、4 项已在 P1 补齐：

1. `Switchable*` / `PauseEffectIf*` / `ColorModifier` / `FxLODSetting` 等原版脚本替身；
2. `fxparams.json` 已通过 P1 扩展为 raw 存档（Switchable* / PauseEffectIf* / ColorModifier / FxLODSetting 等），但这些类的执行逻辑仍未实现；
3. `ApplyMissingScriptFallback` 仍可能把未知 Missing Script 误判为 UVTweener；
4. UV 替身组件在对象池复用时的 offset/ST 复位（已在 P1 完成，见 [27-ArkFx-P1池化与参数管线](27-ArkFx-P1池化与参数管线.md)）；
5. `ArkFxEffectHost.Finished` 与 `Effect` 生命周期完全合并（目前仍以 `LifeTime` 为回收上限）；
6. `GetLine` 的 AB 来源尚未 Kick。

这些问题按 [24-ArkFx系统现状与能力边界](24-ArkFx系统现状与能力边界.md) 的优先级继续推进。
