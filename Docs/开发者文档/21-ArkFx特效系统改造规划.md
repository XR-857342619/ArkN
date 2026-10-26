# 21 · ArkFx 特效系统改造规划

> 基于 IDA 静态分析原版 `GameAssembly_old.dll` 的实证结论（见 [arkfx_re/README.md](arkfx_re/README.md)）。
> 编写时间：方案 A（重打包）已落地之后；本文档是「方案 B/C」的实施蓝图。
> 状态：**P0 + P1 已实现**（2026-10-25），验收见 [22-ArkFx-P0P1验收手册](22-ArkFx-P0P1验收手册.md)；P2 起待开工。

---

## 0. 结论摘要

| 问题 | 结论 |
|---|---|
| 现在能不能开始改？ | **可以**。渲染层（UV）已 100% 实证，可以先做；行为层分阶段跟进 |
| 一次全改还是分批？ | **分批**。按「渲染 → 时间 → 发射 → 挂点 → 表简化」5 个阶段，每阶段可独立验收/回滚 |
| 最大风险 | 破坏性替换（`ArkFxUVTween` v5 的 `_UVTween` 理论已被推翻），必须可回滚 + 可 A/B |
| EffectData 能简化吗 | **能**，12 字段 → 6 字段，但前提是先完成 P4（挂点枚举映射） |

---

## 1. 已解明的原版机制（累计）

详细伪代码见 [arkfx_re/README.md](arkfx_re/README.md)，此处只列要点。

### 1.1 渲染层

**`Torappu.Fx.FxUVTweener`（333 例）**
```
OnEnable: m_v2 = keepInitOffset ? material.mainTextureOffset : 0
Update:   m_v2 += Time.deltaTime * (xspeed, yspeed) * 2f
          if (protectMainUV) m_v2 -= floor(m_v2)          // 包裹到 [0,1)
          material.mainTextureOffset = m_v2
          // 第二贴图：
          m_secondMapST.zw += dt * (secondXSpeed, secondYSpeed) * 2f
          material.SetVector(secondMapName + "_ST", m_secondMapST)
```

**`Torappu.Fx.FxUVTweenerAdvance`（72 例）** —— 同上，但：
- `tweenRepeat`(bool) 为真 → 无限循环并取小数包裹；为假 → 只跑 `tweenAnmTime` 秒后停
- 额外的 `extraMapSettings` 列表：每项 `{enabled, xSpeed, ySpeed, st(Vector4), mapName}`，写 `mapName + "_ST"`

**`Torappu.Fx.FxUVRotation`（111 例）**
```
LateUpdate: _ApplyRotationProperties(1.0f)
  renderer.GetPropertyBlock(mpb)
  mpb.SetFloat("_HG_UV_ROTATION", 1.0f)
  mpb.SetVector(_Rotation0, rotateTex1 ? Vector4(cos, -sin, sin, cos) : IDENTITY)
  mpb.SetVector(_Rotation1, rotateTex2 ? ... : IDENTITY)
  mpb.SetVector(_Rotation2, rotateTex3 ? ... : IDENTITY)
  mpb.SetVector(_Rotation3, rotateTex4 ? ... : IDENTITY)
  renderer.SetPropertyBlock(mpb)
```
> `_propertyName` 默认 `"_Rotation"`，四个向量属性 = `_propertyName + "0..3"`。
> 用 **MaterialPropertyBlock**，不污染材质。

### 1.2 时间层

**`Torappu.Fx.FxDelay`（145 例）** —— 延迟激活
```
OnEnable: if (!m_isWaiting) { m_isWaiting = true; gameObject.SetActive(false);
                               Invoke("_DelayFunc", _delayTime / playbackSpeed); }
_DelayFunc: if (m_isWaiting) { gameObject.SetActive(true); m_isWaiting = false; }
ForceToEnd: 若仍处于隐藏态 → CancelInvoke + 立即激活
```
> 注意：它把**自己所在的 GameObject 整个隐藏**，所以该节点下的粒子/子节点全部延后启动。

### 1.3 发射器层

**`OnPlayEmitter`（40 例）** 字段：`_effects[], _instant, _preDelay, _clearWhenFinish, _keepPauseSync, _useFaceVector, _useSourceEffectPos`
```
_OnPlayInternal:
    if (_instant) _DoEmit(); else if (gameObject.activeSelf) StartCoroutine(WaitPreDelay → _DoEmit)
_DoEmit:
    foreach (name in _effects)
        e = BattleController.CreateEffect(name, pos, owner)  // 3 种重载按 _useSourceEffectPos/_useFaceVector 选
        记录到 m_effects
OnFinish:
    if (_clearWhenFinish) 销毁 m_effects 里的所有实例
```

**`OnFinishEmitter`（5 例）** 字段：`_effects[], _useEffectPosition, _useFaceVector` —— 同上，触发点是 OnFinish

**`RandomEmitter`（3 例）** 字段：`_count, _delayPerEffect, _effect, _preDelay, _randomRangeFrom, _randomRangeTo`
—— 在 `[randomRangeFrom, randomRangeTo]` 里随机取点，按 `_delayPerEffect` 间隔发射 `_count` 个

**`OnPlayEmitterIf`（2 例）** —— 在 OnPlayEmitter 基础上增加条件（buff / 朝向 / motionMode）

> 关键：`_effects` 是**特效名字符串**，通过 `GatherEffects()` 解析成 `ObjectPtr<Effect>`。
> 这对我们是好消息：**ArkFx 直接用 `ArkFxLoader.LoadPrefabByName(name)` 就能实现**，不需要原版资源表。

### 1.4 挂点层（本次解明的重点）

**`FollowMountPoint._mountPointType` 枚举值**（从 `Unit.GetMountPointForEffect` + `Entity.GetMountPoint` 反编译得出）

| 值 | 含义 | 实现来源 |
|---|---|---|
| 0 | 脚底 | `Entity.footMountPoint` |
| 1 | 受击点 | `Entity.hitMountPoint` |
| 2 | 枪口 / 发射点 | `Entity.muzzleMountPoint` |
| 3 | 头部 | `Entity.headMountPoint` |
| 4 | UI 点 | `Entity.uiMountPoint` |
| **5 ~ 29** | **Spine 骨骼索引** | `animator.GetBoneTransform(i)`（索引进骨架骨骼数组） |
| 7 | Entity 根节点 transform | 特殊分支 |
| ≥30 | 抛 `ArgumentOutOfRangeException` | — |

分派逻辑（`Unit.GetMountPointForEffect`）：
```csharp
if ((mp == 7 || mp <= 4 || mp >= 29) && mp != 29) return Entity.GetMountPoint(mp);  // 标准点
else return _LazyLoadMountPoint(animator.GetBoneTransform(mp));                     // Spine 骨骼索引
```

实测分布（144 个 `FollowMountPoint` 实例）：`5`：81、`6`：18、`3`：11、`8`：9 …
→ **绝大多数用的是 Spine 骨骼索引**，不是标准挂点。

**`FollowMountPoint` 其余字段**（实测基本是默认值）
`_followScale`（全 0）、`_scaleMultiplier`（全 1.0）、`_followScaleSign`（142/144 为 0）、
`_pauseWhenMountPointInvalid`（127/144 为 1）、`_keepOriginRotation`（10 例为 1）、`_useBehaviourPause`（4 例为 1）

**`FollowMountPoint` 行为**：`OnPlay` / `LateUpdate` 每帧把特效摆到 `owner.GetMountPointForEffect(_mountPointType)` 的位置；
挂点无效时按 `_pauseWhenMountPointInvalid` 决定是否暂停。

### 1.5 生命周期（部分解明）

- `ParticleEffect.DoPlay()` 实质只有 `base.DoPlay(); particleSystem.Play(withChildren: true);`
- 基类 `Effects.Effect` 提供 `OnPlay / OnFinish / OnPause / OnRecycle / GatherEffects / FinishEffects` 骨架
- `ParticleEffect._CheckIfAlive()` 决定自然结束条件（**尚未细读，P4 阶段补**）

---

## 2. 现状差距矩阵

| 能力 | 原版 | ArkFx 现状 | 差距 |
|---|---|---|---|
| 粒子播放 | `Play(withChildren:true)` | `Kick()` 里 `ps.Play(true)` | ✅ 等价 |
| UV 滚动 | ×2、`mainTextureOffset`、`<name>_ST` | ×1、写 `_UVTween` | ❌ **错** |
| UV 旋转 | PropertyBlock + 4 矩阵 | 无 | ❌ 缺失 |
| 延迟启动 | `FxDelay` SetActive 延时 | 无（一上来全播） | ❌ 缺失 |
| 子特效发射 | Emitters + `CreateEffect(name)` | 无 | ❌ 缺失 |
| 挂点跟随 | `_mountPointType` 枚举 | `EffectData.BindPoint` 字符串 | ⚠️ 词汇表不通 |
| 生命周期 | `Effect` 基类状态机 | `Effect.cs` + `EffectData.LifeTime` | ⚠️ 体系不同 |
| 左右/背向 | `Switchable*` | `EffectData.ForwordDirection` | ⚠️ 部分覆盖 |
| 暂停条件 | `PauseEffectIf*` | 无 | ❌ 缺失 |

---

## 3. 改造目标与验收标准

**目标**：在不动游戏玩法逻辑的前提下，让方舟特效的**观感**与原版一致（≥90% 的 prefab 无肉眼可辨差异）。

**量化验收**（对应 `Tools/arkfx_smoke.py` + 新工具）：

| 指标 | 当前 | 目标 |
|---|---|---|
| 材质/Shader/贴图/网格可解析率 | 100% | 保持 100% |
| UV 行为正确的 prefab | 0（全部偏） | 210（100%） |
| `FxDelay` 生效的 prefab | 0 | 145 |
| 子特效正常发射的 prefab | 0 | 48 |
| 挂点语义正确的 prefab | 未知 | 143 |
| 代表特效 A/B 录屏差异 | 未测 | 抽 20 个，肉眼无差异 |

---

## 4. 改造方案（5 个阶段）

### P0 · 基础设施（前置，必做）

现状问题：`ArkFxLoader.Kick()` 是「一把梭」——实例化后把所有粒子 Play 一遍，没有生命周期概念。
而原版的 Emitters / FxDelay / Switchable* 都依赖 `OnPlay / OnFinish / OnPause / OnRecycle` 时序。

**新建 `ArkFxBehaviour` 基类**（对应原版 `Torappu.Battle.Effects.Effect` 的骨架）：
```csharp
public abstract class ArkFxBehaviour : MonoBehaviour
{
    public virtual void OnPreload() {}   // 包加载后、实例化前
    public virtual void OnPlay() {}      // 播放（对应 Kick 的时机）
    public virtual void OnFinish() {}    // 自然结束
    public virtual void OnPause(bool p) {}
    public virtual void OnRecycle() {}   // 回池
    public float  PlaybackSpeed = 1f;
}
```
**新建 `ArkFxEffectHost`**：挂在每个 AB 特效实例根节点，负责
- `Play()`：按顺序调所有 `ArkFxBehaviour.OnPlay()`
- 结束判定：`ParticleSystem` 全部 `!IsAlive(true)` 且无 `loop` → `OnFinish()`
- 与现有 `Effect.cs` 的生命周期对接（`EffectData.LifeTime` 作为**上限**，不是唯一依据）

**为什么必须做**：没有这个骨架，Emitters/FxDelay 写了也无处挂。

### P1 · 渲染层（已完全实证，可立即开工）

**新建 `ArkFxUVTweenV6.cs`**（替换 `ArkFxUVTween`）
```csharp
// 逐字段对应原版，无猜测
void OnEnable() {
    m_v2 = keepInitOffset ? mat.mainTextureOffset : Vector2.zero;
    if (useSecondMap) { m_secondMapST = keepInitOffset ? mat.GetVector(secondMapSTProp) : ...; }
}
void Update() {
    m_v2 += Time.deltaTime * new Vector2(xspeed, yspeed) * 2f;
    if (protectMainUV) m_v2 -= new Vector2(Mathf.Floor(m_v2.x), Mathf.Floor(m_v2.y));
    mat.mainTextureOffset = m_v2;
    ...第二贴图同上，写 secondMapName + "_ST"...
}
```
`ArkFxUVTweenAdvanceV6.cs` + `ArkFxUVTweenMapSetting`（`tweenRepeat` 语义 + `extraMapSettings`）

**新建 `ArkFxUVRotation.cs`**
```csharp
void LateUpdate() {
    renderer.GetPropertyBlock(mpb);
    mpb.SetFloat(RotationId, 1f);
    mpb.SetVector(Rot0, rotateTex1 ? Mat(angle1) : IDENTITY);
    ... 共 4 组 ...
    renderer.SetPropertyBlock(mpb);
}
static Vector4 Mat(float deg) { var r = deg * Mathf.Deg2Rad;
    return new Vector4(Mathf.Cos(r), -Mathf.Sin(r), Mathf.Sin(r), Mathf.Cos(r)); }
```

**接线方式**：`ArkFxLoader` 按 `fxparams.json` 里的 `cls` 字段决定挂哪个组件（已有框架，仅需扩展 `FxUVRotation` 分支）。
**开关**：`ArkFxLoader.UseV6 = true/false` 全局静态开关，便于 A/B 与回滚。

### P2 · 时间层

**新建 `ArkFxDelay.cs`** —— 逐字复刻：
```csharp
void OnEnable() { if (isWaiting) return; isWaiting = true; gameObject.SetActive(false);
                  Invoke(nameof(DelayFunc), delayTime / playbackSpeed); }
void DelayFunc() { if (!isWaiting) return; gameObject.SetActive(true); isWaiting = false; }
public void ForceToEnd() { ... }
```
> ⚠️ 与 P0 的 `ArkFxEffectHost` 有交互：被隐藏的节点不应被 host 判定为"已结束"。

### P3 · 发射器层

**新建 `ArkFxEmitter.cs`（基类）+ 4 个子类**：
`ArkFxOnPlayEmitter` / `ArkFxOnFinishEmitter` / `ArkFxRandomEmitter` / `ArkFxOnPlayEmitterIf`

```csharp
// 通用发射
GameObject Emit(string effectName, Vector3 pos, Quaternion rot) {
    var go = ArkFxLoader.LoadPrefabByName(effectName);
    if (go == null) { Debug.LogWarning($"[ArkFx] 子特效缺失: {effectName}"); return null; }
    var inst = Instantiate(go, pos, rot);
    ArkFxLoader.Kick(inst, effectName);      // 递归驱动
    spawned.Add(inst);
    return inst;
}
```
- `OnPlayEmitter`：`_instant` → 立即；否则协程等 `_preDelay`
- `OnFinishEmitter`：在 host 的 `OnFinish` 时触发
- `RandomEmitter`：范围随机 + `_count` 个 + `_delayPerEffect` 间隔
- `_clearWhenFinish` → 结束时销毁 spawned

> **依赖**：`_effects` 里的名字必须在已打包的 arkfx 里能查到。
> 需要用 `Tools/arkfx_merge.py` 把「被引用的子特效名」也纳入打包范围（见 §5.3）。

### P4 · 挂点层（决定 EffectData 能否简化）

**新建 `ArkFxMountPoint`（静态工具）**
```csharp
public enum MountPointType { Foot=0, Hit=1, Muzzle=2, Head=3, UI=4, SelfTransform=7, SpineBone=100 };
// 5~29 一律按 Spine 骨骼索引处理
public static Transform Resolve(Unit owner, int mp) {
    switch (mp) {
        case 0: return owner.FootPoint;  case 1: return owner.HitPoint;
        case 2: return owner.MuzzlePoint; case 3: return owner.HeadPoint;
        case 4: return owner.UIPoint;     case 7: return owner.transform;
        default: return owner.GetSpineBoneByIndex(mp);   // 5..29
    }
}
```
**前置条件（必须先验证）**：你们工程里的 Spine 骨架必须与原版**同版本同骨骼顺序**，否则骨骼索引会错位。
验证方法：抽 5 个干员，比对 `skeleton.bones[i].name` 与原版 `_mountPointType` 的实际落点（用 `ArkFxDiag` 打印）。

若骨骼顺序不可靠 → **降级方案**：继续用字符串 `BindPoint`，但把 `_mountPointType` 的索引 dump 成对照表，人工映射一次。

### P5 · EffectData 简化（12 → 6）

```csharp
public class EffectData : IConfig
{
    public string Id;                  // 保留
    public string Prefab;              // 保留
    public int    SpawnMode = 0;       // 0=世界坐标 1=跟随施法者 2=跟随目标（取代 StartPos + ParentFollow 部分）
    public Vector3 Offset;             // 仅作微调，默认 0
    public int    MountPoint = -1;     // -1 = 用 prefab 自带的 _mountPointType
    public float  ScaleXFollow;        // 保留（你们 2D 镜像需求，原版无对应）
    public float  FaceCamera;          // 保留（你们相机朝向方案，原版无对应）
    public float  LifeTime = 0;        // 0 = 自动（粒子时长 + _delayToFinish）
}
```
**删除**：`StartPos`、`BoneFollow`、`ForwardOnly`、`ParentFollow`、`ForwordDirection`
**迁移**：写 `Tools/migrate_effectdata.py`，按 prefab 的实际组件自动填新字段；保留旧字段做兼容读取一个版本。

---

## 5. 代码结构设计

### 5.1 新增文件清单

```
Assets/Scripts/Core/Effect/
├── ArkFxBehaviour.cs            P0  行为基类
├── ArkFxEffectHost.cs           P0  生命周期宿主
├── ArkFxUVTweenV6.cs            P1  UV 滚动（替换 v5）
├── ArkFxUVTweenAdvanceV6.cs     P1  UV 滚动（Advance）
├── ArkFxUVRotation.cs           P1  UV 旋转
├── ArkFxDelay.cs                P2  延迟激活
├── ArkFxEmitter.cs              P3  发射器基类
├── ArkFxOnPlayEmitter.cs        P3
├── ArkFxOnFinishEmitter.cs      P3
├── ArkFxRandomEmitter.cs        P3
├── ArkFxMountPoint.cs           P4  挂点解析
└── ArkFxScriptStub.cs           保留：未实现类的存档占位
```

### 5.2 与现有代码的接线点

| 位置 | 改动 |
|---|---|
| `ArkFxLoader.BakePrefab/ Kick` | 由「挂 UVTween + Play 粒子」改为「挂对应 ArkFxBehaviour 子类 + 调 ArkFxEffectHost.Play()」 |
| `ArkFxLoader.ApplyFxParams` | 扩展 `cls` 分支：`FxUVRotation / FxDelay / OnPlayEmitter / ...` |
| `EffectManager.GetEffect` | 不变（仍走 `LoadPrefabByName` + `Effect` 组件） |
| `Effect.cs` | `LifeTime` 语义改为"上限"；`Init/Update` 与 host 对齐 |

### 5.3 打包侧配套

`Tools/arkfx_merge.py` 需要扩展一步：**把 emitter 引用的子特效名也纳入 `used` 集合**（递归展开），
否则 `OnPlayEmitter._effects` 里的名字可能落在未打包的包里。

---

## 6. 风险与回滚

| 风险 | 影响 | 对策 |
|---|---|---|
| v6 与 v5 行为差异导致"现在能看的变难看" | 高 | `ArkFxLoader.UseV6` 全局开关 + prefab 级 override |
| Spine 骨骼索引错位 | 中 | P4 前先做骨骼顺序校验；失败则退回字符串 BindPoint |
| 生命周期改动影响对象池 | 中 | `ArkFxEffectHost` 只做「通知」，实际回收仍由 `Effect.cs` 决定 |
| 子特效缺失（未打包） | 中 | 打包脚本递归展开 + 运行时 `LogWarning` 不崩 |
| 性能（每帧 PropertyBlock / 多协程） | 低 | PropertyBlock 只在 `_rotateTex*` 有变化时写；协程数量已知（145+48） |

**已发生的风险（已修复）**：UnityPy 默认写出 UnityFS **format 8**，Unity 2021.2 只认 **format 7**，
进 Play 会报 `Unable to read header from archive file`。修复见
[22-ArkFx-P0P1验收手册 §10](22-ArkFx-P0P1验收手册.md)：`arkfx_merge.py` 改为按 2021.2 布局手写容器。

**回滚策略**：全部新增文件独立、不修改旧文件的行为路径（只用开关切），删文件 + 关开关即恢复。

---

## 7. 验收与回归

1. **静态验收**（每次改完跑）
   ```bash
   python Tools/verify_arkfx_ab.py --ab-dir Assets/StreamingAssets/arkfx --header-only
   python Tools/arkfx_smoke.py --all
   ```
2. **运行时验收**：抽 20 个代表特效（覆盖 Disturb / Dissolve / 多贴图 / 旋转 / 延迟 / 发射器 各系），
   `ArkFxDiag.Dump()` 输出材质属性快照，与原版录屏逐帧对比。
3. **回归清单**：每次改完至少回归
   - `aglina_skill_02_start`（多贴图 + 旋转）
   - `amiya_skill_03_start`（Dissolve）
   - `surtr_skill_01_start`（多发射器）
   - `common_ignite_defense`（延迟 + 挂点）

---

## 8. 里程碑与工作量估算

| 阶段 | 内容 | 估算 | 依赖 |
|---|---|---|---|
| P0 | `ArkFxBehaviour` + `ArkFxEffectHost` | 2 天 | 无 |
| P1 | UVTweenV6 + UVTweenAdvanceV6 + UVRotation | 2 天 | P0 |
| P2 | `ArkFxDelay` | 0.5 天 | P0 |
| P3 | 4 个 Emitter + 打包脚本递归展开 | 2 天 | P0、P1 |
| P4 | `ArkFxMountPoint` + 骨骼顺序校验 | 1.5 天 | 需先验证 Spine 骷髅顺序 |
| P5 | EffectData 简化 + 迁移脚本 + 兼容层 | 2 天 | P4 |
| 验收 | 20 个代表特效 A/B + 回归 | 2 天 | 全部 |

**合计 ≈ 12 人天**（不含剩余的 Switchable\* / PauseEffectIf / ProjectileEffect 等）。

---

## 9. 仍未解明的类（后续阶段）

| 类 | 实例 | 影响 |
|---|---|---|
| `ParticleEffect._CheckIfAlive` | 1078 | 自然结束条件（影响 LifeTime 语义） |
| `SwitchableBackEffect` / `SwitchableRightEffect` / `SwitchableEffectByFaceDirection` | 27 | 左右/背向切换 |
| `PauseEffectIf*` 家族 | 29 | 暂停条件 |
| `ProjectileEffect` | 98 | 弹道类特效 |
| `FollowDirection` | 70 | 朝向跟随 |
| `FxLODSetting` | 35 | 低配降级 |
| `ColorModifier` / `SetAnimatorFloatByCondition` / `BuffProgressFloatSource` | 20 | 颜色/动画器参数 |
| `CameraEffect` / `OnPlayAudioPlayer` | 6 | 震屏 / 音效 |

> 这些**不阻塞** P0~P5；它们各自独立，按「实例数 × 视觉影响」排序逐个补。

---

## 10. 立即可以开工的部分

- ✅ **P0 + P1**（渲染层已 100% 实证，无未知）
- ✅ **P2**（FxDelay 逻辑完整，5 行代码）
- ✅ **P3**（Emitters 需要打包脚本先支持递归展开）
- ⚠️ **P4/P5** 需要先做一次 Spine 骨骼顺序校验（半天）
