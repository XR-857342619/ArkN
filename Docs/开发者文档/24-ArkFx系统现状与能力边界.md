# 24 · ArkFx 系统现状与能力边界

> 编写时间：2026-10-26（Unity 升级 + P0~P3 完成、P4 证伪之后）
> 用途：接手维护时**第一份该读的文档**——它说明这套系统是什么、能做什么、做不到什么。

---

## 1. ArkFx 是什么

**一句话**：把「明日方舟客户端里的原版特效 AB」搬进本工程并跑起来的一套运行时兼容层。

方舟的 `battle/prefabs/effects/*.ab` 里的 prefab 有三个"离开方舟就跑不动"的问题：

1. 节点上挂的是 `Torappu.Fx.FxUVTweener` 这类**游戏本体程序集**里的 MonoBehaviour → 在本工程全是 `Missing Script`
2. 大量粒子是 `playOnAwake = false`，靠方舟的特效管理器手动 Play → 裸 `Instantiate` 出来是一堆静止的死模型
3. 自定义 shader（`Torappu/Particles/*`）读的是私有材质属性（`_UVTween` / `_Rotation0..3` / `<贴图>_ST`）

ArkFx 对这三件事逐条做替身。

---

## 2. 数据管线（离线 → 运行时）

```
D:\UnityWork\解包\Windows\battle\prefabs\effects\*.ab      ← 方舟原始包（570 个，LZ4AK 自定义压缩）
        │
        │  Tools/arkfx_merge.py
        │    1. 用 UnityPy + LZ4AK 补丁解开
        │    2. 按 EffectData/BulletData 表挑出用得到的包（75 个）
        │    3. 递归纳入发射器引用的子特效
        │    4. 解析并打包全部依赖（refs/fx/*、[uc]shaders.ab、anon 默认材质包…）
        │    5. 用「Unity 2021.2 兼容容器」重写（format 7 + 内联 blocksInfo + 16 字节对齐 + LZ4HC）
        ▼
Assets/StreamingAssets/arkfx/                     ← 101 个标准 AB（主包 76 + 依赖 25）
        ├── effects/*_lz4.ab                      1193 个 prefab
        ├── _deps/<8位hash>_lz4.ab
        ├── _manifest.json                        构建清单（prefab → 包）
        └── prefab清单.md

        │  Tools/dump_fx_params.py
        │    从 AB 内嵌 typetree 直接读出原版脚本的精确字段值
        ▼
Assets/StreamingAssets/arkfx/fxparams.json        ← 430 个 prefab / 826 条参数
```

> `StreamingAssets` 在 `.gitignore` 里，**这些产物需要本地跑脚本生成**。

---

## 3. 运行时架构

```
ArkFxBootstrap (RuntimeInitializeOnLoadMethod / BeforeSceneLoad)
  └─ ArkFxLoader.LoadAll(StreamingAssets/arkfx)
       ├─ 第一遍：依赖包（_deps、shader、<hash>_lz4.ab）
       ├─ 第二遍：主包
       └─ LoadParams()  读 fxparams.json

EffectManager.GetEffect(id) / BulletManager.Get(model)
  ├─ 先找工程内 Assets/Bundles/Effect/<name>（Addressables）
  ├─ 找不到 → ArkFxLoader.LoadPrefabByName(name)   ← AB 兜底
  │            └─ BakePrefab：ApplyFxParams / 缺脚本兜底 / 亮度 / 挂 ArkFxEffectHost
  └─ Pool.Spawn 实例化
       └─ ArkFxLoader.Kick(instance, name)
            └─ ArkFxEffectHost.Play()
                 ├─ 广播所有 ArkFxBehaviour.OnPlay()
                 └─ 把所有 ParticleSystem.Play(true)

每帧：
  ArkFxEffectHost.Update()     结束判定（粒子全灭 + 无组件在等待 → OnFinish）
  ArkFxTicker.Update()         驱动 FxDelay 这类"节点被隐藏后仍要计时"的组件
  各组件自身 Update/LateUpdate UV 滚动、旋转
```

### 组件一览

| 组件 | 对应原版类 | 职责 |
|---|---|---|
| `ArkFxBehaviour` | — | 替身组件基类（OnPlay/OnFinish/OnRecycle/IsBusy） |
| `ArkFxEffectHost` | `Effects.Effect` 骨架 | 生命周期宿主：统一驱动、结束判定、倍速/暂停/回收广播 |
| `ArkFxTicker` | 游戏全局调度器 | 不受激活状态影响的时钟（给 FxDelay 用） |
| `ArkFxUVTweenV6` | `Torappu.Fx.FxUVTweener` | UV 滚动 |
| `ArkFxUVTweenAdvanceV6` | `FxUVTweenerAdvance` | UV 滚动（循环/限时 + 额外贴图） |
| `ArkFxUVRotation` | `FxUVRotation` | UV 旋转（MaterialPropertyBlock） |
| `ArkFxDelay` | `FxDelay` | 节点延迟激活 |
| `ArkFxOnPlayEmitter` | `OnPlayEmitter` / `OnPlayEmitterIf` | 播放时发射子特效 |
| `ArkFxOnFinishEmitter` | `OnFinishEmitter` | 结束时发射 |
| `ArkFxRandomEmitter` | `RandomEmitter` | 范围内随机 + 间隔多发 |
| `ArkFxScriptStub` | 其余 26 类 | 类名 + 原始参数存档（不执行） |

---

## 4. 已实现的语义（全部有 IDA 反编译 + 真机实测双重证据）

| 类 | 关键语义 | 验证方式 |
|---|---|---|
| `FxUVTweener` | 速度 **×2**、写 `mainTextureOffset`、第二贴图写 `<名>_ST`、`protectMainUV` = 包裹到 `[0,1)`、`keepInitOffset` 读材质初值 | 实测 0.5 → 1.0/秒；有/无回绕形成正确反差 |
| `FxUVTweenerAdvance` | `tweenRepeat`(bool) 为真则无限循环并包裹，否则只跑 `tweenAnmTime` 秒；额外贴图列表 | 参数与 Diag 输出一致 |
| `FxUVRotation` | `MaterialPropertyBlock` + `_HG_UV_ROTATION`(float) + `_Rotation0..3`(矩阵 `(cos,-sin,sin,cos)`)，未启用写 IDENTITY | 实测 `(0,-1,1,0)` / `(1,0,0,1)` |
| `FxDelay` | 隐藏自身 GameObject，延时后重新激活；`IsBusy` 让宿主不误判结束 | 实测延迟 1 秒后弹出 |
| Emitters | 按名字加载子特效、随机/间隔/结束时发射、`clearWhenFinish` 清理 | `agoat2_skill_03_buff_01` 发射 5 个子特效 |

---

## 5. 能力边界（**重点**）

### 5.1 环境依赖

| 项 | 要求 | 说明 |
|---|---|---|
| Unity 版本 | **2021.3.39f1** | 素材包用该版本编译 shader；2021.2 下 196 个 shader 只有 2 个可用（全部品红） |
| 平台 | **仅 PC/Standalone** | 实现用 `Directory.GetFiles` + `AssetBundle.LoadFromFile`，安卓 StreamingAssets 在 APK 内，**整段失效** |
| 渲染管线 | Built-in | 方舟 shader 是 Built-in RP 编写 |
| 图形 API | D3D11 | 包内只有 D3D11 变体 |

### 5.2 功能覆盖

| 指标 | 数值 |
|---|---|
| 支持的 AB | 101 个（76 主包 + 25 依赖），1193 个 prefab |
| 有参数驱动的 prefab | 430 个（`fxparams.json`） |
| 原版脚本类 | 34 种 / 2261 个实例 |
| **已实现替身** | **8 种 / 711 个实例（31.4%）** |
| 表内特效命中 | EffectData 343 → 330；BulletData 57 → 52 |

### 5.3 明确做不到的

| 项 | 原因 | 影响 |
|---|---|---|
| `SwitchableBackEffect` / `SwitchableRightEffect` / `SwitchableEffectByFaceDirection`（27） | 未实现 | 单位朝向不对时显示错误的左右/背向特效 |
| `PauseEffectIf*` 家族（29） | 未实现 | 该暂停的时候仍在播 |
| `ColorModifier`（11） | 未实现 | 颜色叠加缺失 |
| `FxLODSetting`（35） | 未实现 | 无低配降级 |
| `FollowMountPoint`（144） | **已证伪**：`_mountPointType` 非骨骼索引也非语义枚举，资产侧无法推导 | 由 `EffectData.BindPoint` 表驱动替代；8 个未填表的 prefab 需人工补 |
| `ParticleEffect`（1078） | 其 `DoPlay()` 实质只有 `particleSystem.Play(true)`，已被 `Kick` 覆盖 | 其 26 个字段里的"何时结束/命中处理"由 `EffectData.LifeTime` + `Effect.cs` 接管 |
| `OnPlayEmitterIf` 的条件判定 | 只实现了无条件发射 | 2 个实例可能在不该发射时发射 |
| 9 个引用不到的父特效 | 源资产里不存在（客户端版本差异） | 运行时打 Warning 跳过 |
| 18 个表内特效名 | 源包里没有（工程自建名） | 由工程内 Addressables 兜底 |

### 5.4 已知"看起来像 bug 但正常"的现象

| 现象 | 原因 |
|---|---|
| Console 大量 `The referenced script ... is missing!` | AB prefab 里是原版脚本，**这正是 ArkFx 存在的前提** |
| Console `Missing built-in guistyle ToolbarSeach*` | 编辑器 UI 皮肤噪音，打包后无 |
| Diag 里 `tex _XxxTex: <未赋值>` | 材质本来就没给该属性赋值（pathID=0），走 shader 默认值 |

---

## 6. 后续可优化清单（按价值排序）

### 6.1 高价值

| # | 项 | 实例数 | 说明 |
|---|---|---|---|
| 1 | `Switchable*` 家族 | 27 | 实现朝向切换：`_UpdateFace` 判定 + 切换子特效；需先解明 `SwitchableBackEffect.OnPlay/_UpdateFace` |
| 2 | `PauseEffectIf*` 家族 | 29 | 实现暂停条件；同时把 `ArkFxEffectHost.SetPaused` 接到战斗暂停 |
| 3 | 战斗暂停同步 | — | 目前宿主有 `SetPaused` 但没有调用方；战斗暂停时特效仍在播 |

### 6.2 中价值

| # | 项 | 实例数 | 说明 |
|---|---|---|---|
| 4 | `ColorModifier` | 11 | `_color` 叠加到所有 Renderer |
| 5 | `FxLODSetting` | 35 | 低配降级（`_disableObjects` / `_particleDetailsHookers`） |
| 6 | 剥离 AB prefab 里的 Missing Script | — | 重打包时用 UnityPy 删掉原版 MonoBehaviour，可消除 Console 刷屏 |

### 6.3 低价值 / 可选

| # | 项 | 说明 |
|---|---|---|
| 7 | `ProjectileEffect`（98） | 弹道逻辑已由 `BulletManager` 接管，多半无需实现 |
| 8 | `FollowDirection`（70） | 朝向跟随，与 `EffectData.ForwordDirection` 部分重叠 |
| 9 | 安卓支持 | 需把加载层改成 `UnityWebRequestAssetBundle` 或先拷到 `persistentDataPath` |
| 10 | 全量打包 | `arkfx_merge.py --all` 可把 570 个包全打进来（约 250MB） |

### 6.4 工程债

| # | 项 | 说明 |
|---|---|---|
| 11 | `StreamingAssets/arkfx` 未纳入版本管理 | 每人需本地跑 `arkfx_merge.py` + `dump_fx_params.py`（约 2 分钟） |
| 12 | Gitee 仓库超配额（1.17GB > 1GB） | 已改用 GitHub；如需回 Gitee 需 `git filter-repo` 清历史 |
| 13 | `fxparams.json` 与 AB 版本耦合 | 重新打包 AB 后必须重跑 dump，否则参数错位 |

---

## 7. 日常维护速查

| 场景 | 操作 |
|---|---|
| 换了 Unity 版本 | 先跑 `Tools/ArkFx/4` 看 shader 是否 `supported` |
| 重新打包 AB | `python Tools/arkfx_merge.py --force` → `python Tools/dump_fx_params.py` |
| 特效看不见 | `Tools/ArkFx/2` 打印 Diag：看 `shader supported` / `材质 null` / `ParticleSystem isPlaying` |
| 特效不动 | 看 Diag 里 `[ArkFx]` 段有没有对应组件；`Tools/ArkFx/6` 采样 UV 偏移 |
| 子特效不出 | Diag 里 `已发射子特效=N`；Console 找 `子特效缺失` |
| 切回工程内特效 | `Tools/ArkFx/0`（PlayerPrefs 持久化） |
