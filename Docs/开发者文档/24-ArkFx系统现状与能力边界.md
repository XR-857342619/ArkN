# 24 · ArkFx 系统现状与能力边界

> 编写时间：2026-10-26（Unity 升级 + P0~P3 完成、P4 证伪之后）
> 用途：接手维护时**第一份该读的文档**——它说明这套系统是什么、能做什么、做不到什么。

---

## 0. 最近变更：P0 新旧生命周期对接修复（2026-10-28）

本轮修复了表驱动特效进入 ArkFx 时的时序问题，详见
[26-ArkFx-P0回归修复与验收](26-ArkFx-P0回归修复与验收.md)。

| 修复 | 变化 |
|---|---|
| Kick 时机 | `EffectManager.GetEffect` / `BulletManager.Get` 不再在零位置 Kick；由 `Effect.Play()` 在 `Init()` 定位后首次启动 |
| FxDelay | ArkFx 实例的 `Effect.Play()` 不再强行 `SetActive(true)`，延迟不会被抵消 |
| LifeTime 默认值 | `EffectData.LifeTime = 5f`，并在 Excel 导出生成器中加入默认值表，避免再次被覆盖 |
| OnFinishEmitter | 覆盖 `OnFinish()`：只发射尾段，不再被基类 `clearWhenFinish` 同帧清空 |
| 共享 prefab | `EffectManager` 对 `AddComponent<Effect>()` 判重，避免多条 EffectData 共用 prefab 时重复挂组件 |
| 池化清理 | `Effect.Awake` 收集 inactive 粒子/拖尾，减少复用残留 |

> ⚠️ 这一轮**没有**新增 `Switchable*` / `PauseEffectIf*` 等替身类；后续路线仍见 §6。

### 0.1 P1 补充（2026-10-28）

- `ArkFxUVTweenV6` / `ArkFxUVTweenAdvanceV6` 已加入对象池复用的 offset/ST 初始快照与 `OnRecycle()` 复位。
- `dump_fx_params.py` 已把 `Switchable*` / `PauseEffectIf*` / `ColorModifier` / `FxLODSetting` / `ProjectileEffect` / `FollowDirection` 等未实现类的 typetree 写入 `raw` 字段。
- `ArkFxScriptStub` 现在支持同一节点多条 `entries`，并新增独立的 `baked` 标记。
- 详见 [27-ArkFx-P1池化与参数管线](27-ArkFx-P1池化与参数管线.md)。

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
Assets/StreamingAssets/arkfx/fxparams.json        ← 9410 个 prefab / 17753 条参数（2026-10-28）
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
| 平台 | **PC / Android 均可**（Android 需走 persistentDataPath 拷贝，见 §5.5） | 见 §5.5 |
| 渲染管线 | Built-in | 方舟 shader 是 Built-in RP 编写 |
| 图形 API | D3D11 | 包内只有 D3D11 变体 |

### 5.2 功能覆盖

| 指标 | 数值 |
|---|---|
| 支持的 AB | **621 个（570 主包 + 51 依赖），311 MB** |
| 包内 prefab 总数 | **14665 个（方舟原版特效全量）** |
| 有参数驱动的 prefab | **9410 个（`fxparams.json`，17753 条参数；含 3928 条 raw 存档）** |
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
| 4 | `ColorModifier` | 11 | `_color` 叠加到所有 Renderer（raw 参数已存档，逻辑待实现） |
| 5 | `FxLODSetting` | 35 | 低配降级（`_disableObjects` / `_particleDetailsHookers`；raw 参数已存档，逻辑待实现） |
| 6 | 剥离 AB prefab 里的 Missing Script | — | 重打包时用 UnityPy 删掉原版 MonoBehaviour，可消除 Console 刷屏 |

### 6.3 低价值 / 可选

| # | 项 | 说明 |
|---|---|---|
| 7 | `ProjectileEffect`（98） | 弹道逻辑已由 `BulletManager` 接管，多半无需实现 |
| 8 | `FollowDirection`（70） | 朝向跟随，与 `EffectData.ForwordDirection` 部分重叠 |
| 9 | ~~安卓支持~~ | ✅ **已完成**（拷贝到 persistentDataPath，见 §5.5） |
| 10 | ~~全量打包~~ | ✅ **已完成**（621 包 / 311MB / 14665 prefab，见 §5.6） |

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

---

## 5.5 Android 适配（2026-10-26 完成）

### 为什么必须特殊处理

| | Addressables（extra catalog） | ArkFx（改造前） |
|---|---|---|
| 怎么发现包 | 读 `catalog.json`（`UnityWebRequest.Get`） | `Directory.GetFiles(dir, "*.ab", AllDirectories)` |
| 怎么加载包 | 本地文件走 `LoadFromFile`；`jar:`/`http` 走 **`UnityWebRequestAssetBundle`** | 直接 `AssetBundle.LoadFromFile(path)` |
| Android 上的 StreamingAssets | `jar:file:///data/app/…/base.apk!/assets/…`，**是 URI 不是文件系统路径** | ❌ 整段失效 |

**结论：差别不在"包在不在外部"，而在读取 API。**
Addressables 把读取抽象成 URL 请求，所以天生支持 APK 内路径；ArkFx 用的是纯文件系统 API。

### 采用的方案：首次启动拷贝到 persistentDataPath

工程里已有 `Assets/Scripts/StreamingAssetsCopyUtility.cs`
（把 `filelist.txt` 列出的文件用 `UnityWebRequest` 从 StreamingAssets 拷到 `PathHelper.AppHotfixResPath`），
直接复用。

| # | 改动 | 文件 |
|---|---|---|
| 1 | `AbDir` 改为 `Path.Combine(PathHelper.AppHotfixResPath, "arkfx")`<br>（桌面端 == StreamingAssets；移动端 == `persistentDataPath/<productName>/`） | `ArkFxLoader.cs` / `ArkFxBootstrap.cs` / `ArkFxSlot.cs` |
| 2 | 拷贝完成后补一次 `ArkFxLoader.LoadAll(AbDir)`<br>（`ArkFxBootstrap` 跑在 `BeforeSceneLoad`，首启时拷贝还没完成；`LoadAll` 幂等） | `Init.cs` §2.6 |
| 3 | 完成标记改为**版本化 key**（`FirstLaunchCopyDone_v2`）<br>AB 内容变更时把 `COPY_VERSION` +1 即可强制重拷 | `StreamingAssetsCopyUtility.cs` |
| 4 | 有文件拷贝失败时**不再标记完成**，下次启动自动重试 | 同上 |
| 5 | 移动端首启「目录不存在」的 Warning 降级为 Log | `ArkFxBootstrap.cs` |

### 前置条件

`Assets/StreamingAssets/filelist.txt` 必须包含 `arkfx` 下 **全部 101 个 .ab**（外加 `fxparams.json` / `_manifest.json` / `prefab清单.md`）。
由菜单 **`Tools/生成StreamingAssets文件列表`** 生成（当前 316 条，其中 arkfx 104 条）。

### 代价与约束

| 项 | 数值 / 说明 |
|---|---|
| APK 体积 | **+77 MB**（StreamingAssets 会打进包） |
| 设备存储 | **再 +77 MB**（persistentDataPath 副本），合计双份 |
| 首次启动 | 101 个 AB 逐个 `UnityWebRequest` 下载写盘，低端机可能数秒~十几秒 |
| 更新 AB 后 | 必须把 `StreamingAssetsCopyUtility.COPY_VERSION` +1，否则客户端仍用旧副本 |
| 内存 | 101 个包一次性 `LoadFromFile`（LZ4HC 压缩态映射），实测 PC 正常；低端机需观察 |

### 未采用的方案（记录备查）

**改为 `UnityWebRequestAssetBundle` 直接从 APK 读**：
省掉 77 MB 双份存储，但 `ArkFxLoader.LoadAll` / `LoadPrefabByName` 需要从同步改异步，
牵动 `EffectManager.GetEffect` / `BulletManager.Get` 的调用链（这两个目前是同步返回 prefab），改造量大。
若将来存储敏感，再评估。

---

## 5.6 全量打包 + AB 优先加载（2026-10-26）

### 为什么改

只按配置表挑包时有个死结：

> **没有任何途径知道原版有哪些特效名** —— 表里没有的名字不会被 `arkfx_merge.py` 打包，
> 于是"能用的特效数"永远等于表里已写的数量，不会增长。

所以改成 **「先全量入库，再用表挑选」**：

```
第 1 步：把所有原版包都打进 arkfx（用 prefab列表.txt 枚举出全部特效名）
第 2 步：想用哪个，就在 Excel 表里填它的 Prefab 名
```

> 另一个动机：`Extra catalog` 加载的另一个项目的 AB 包本质上也是在补特效。
> 如果 ArkFx 的特效足够全，就可以考虑把那个依赖整个砍掉。

### 现状

| 项 | 数值 |
|---|---|
| arkfx 包数 | 621（570 主包 + 51 依赖） |
| 体积 | **311 MB**（StreamingAssets 合计 333 MB） |
| 包内 prefab | **14665 个** |
| 有参数驱动 | 9410 个 / 17753 条（含 3928 条 raw 存档） |
| 表当前引用 | 398 个（380 在包内） |

### 新增产物：`prefab列表.txt`

`Assets/StreamingAssets/arkfx/prefab列表.txt` —— **14665 行，一行一个 prefab 名**，可直接 grep：
```
absin_attack_01_hit
absin_attack_01_hit_epoque#28
aglina_skill_02_start
...
```
另有 `prefab清单.md`（名字 → 所在 ab 包）和 `_manifest.json`（机器可读）。

### 加载优先级改为 AB 优先

```csharp
EffectManager.GetEffect(id):
    if (ArkFxLoader.PreferAbSource)          // 默认 true
        go = ArkFxLoader.LoadPrefabByName(config.Prefab);   // ① 先找原版 AB
    if (go == null)
        go = ResHelper.GetAsset<GameObject>(...);            // ② 再回落工程内
```

`BulletManager.Get` / `GetLine` 同步修改。菜单 0 文案改为「AB优先 ↔ 工程内优先」，
开关存 PlayerPrefs。

**为什么要索引**：全量 621 个包时，`LoadPrefabByName` 若逐个 `LoadAsset` 扫描会非常慢。
现在 `LoadAll` 时建一次 `短名 → 包` 索引（`GetAllAssetNames`），查找 O(1)；
并加了**负缓存**（确认不存在的名字不再全扫）。

### 使用方式：启用任意一个原版特效

```
1. 打开 Assets/StreamingAssets/arkfx/prefab列表.txt，搜到想要的 prefab 名
2. 在 Excel 的 EffectData 表加一行：Id=xxx, Prefab=<那个名字>
3. 导表 → 生成 Data/EffectData/*.txt
4. 让玩法（SkillData/BuffData）在合适时机调 GetEffect(该 Id)
```

**不需要重新打包 arkfx**（全量已在库里），也不需要重跑 `dump_fx_params.py`（参数已按 prefab 名全量导出）。

### 代价

| 项 | 数值 |
|---|---|
| StreamingAssets | 333 MB（原 94 MB → +239 MB） |
| APK | +311 MB |
| 设备存储（安卓拷贝） | 再 +333 MB |
| 首次启动拷贝（安卓） | 文件数 837，其中 621 个 .ab |
| 启动时 AB 注册 | 621 次 `LoadFromFile`（只读头，实测可接受） |
| 内存 | 索引约 14665 个字符串键 |

---

## 5.7 特效来源开关改为存档持久化（2026-10-26）

### 变更

`ArkFxLoader.PreferAbSource` 从 **PlayerPrefs** 改为 **游戏存档 `data.sav`**：

```csharp
public static bool PreferAbSource
{
    get { return GameData.Instance.preferAbEffects; }
    set
    {
        var gd = GameData.Instance;
        if (gd.preferAbEffects == value) return;
        gd.preferAbEffects = value;
        SaveHelper.SaveData();      // 立即落盘
    }
}
```

| 项 | 改动 |
|---|---|
| `GameData` | 新增字段 `public bool preferAbEffects = true;` |
| `ArkFxLoader` | 属性改为读写 `GameData`，移除 PlayerPrefs 键 `ArkFx.PreferAbSource` |
| 主界面设置面板 | 新增按钮 `SwitchEffectsSource` 的点击处理 + `Flush()` 回填状态 |
| 编辑器菜单 `Tools/ArkFx/0` | 同一个开关，文案改为"写入游戏存档" |

### UI 接线（`Assets/Scripts/UI/Main/UI_Main.cs`）

```csharp
// 点击
m_SwitchEffectsSource.onClick.Add(() => {
    ArkFxLoader.PreferAbSource = m_SwitchEffectsSource.selected;   // setter 内部写存档
    TipManager.Instance.ShowTip(m_SwitchEffectsSource.selected ? "特效来源：原版 AB 优先" : "特效来源：工程内优先");
});

// Flush() 里回填
m_SwitchEffectsSource.selected = ArkFxLoader.PreferAbSource;
```

### 注意

- `UseV6`（v5/v6 UV 实现切换）**仍留在 PlayerPrefs**，因为它只是开发期 A/B 用的调试开关，不属于玩家设置。
- `ArkFxBootstrap` 在 `BeforeSceneLoad` 执行，那时 `GameData` 还没 `Init`；
  但 `PreferAbSource` 只在"生成特效"时被读取，此时存档已加载，所以无需额外同步。
- 老存档里没有 `preferAbEffects` 字段 → Newtonsoft 用字段初值 `true`，即默认「AB 优先」，符合预期。

---

## 5.8 双平台特效包（Windows + Android，2026-10-27）

### 为什么必须两套

AssetBundle 是**平台相关**的，三层都会不匹配：

| 层 | Windows 版 | Android 版 |
|---|---|---|
| SerializedFile `target_platform` | `StandaloneWindows64` | `Android` |
| Shader 编译变体 | D3D11（`m_Platforms=[4]`） | GLES3 / Vulkan（`m_Platforms=[9,18]`） |
| 贴图压缩 | DXT / BC | ETC2 / ASTC |

任一不匹配都会导致：加载被拒 / `isSupported=false` 全品红 / 贴图花屏。
所以**不能只拿 Windows 版给安卓用**。

### 目录约定

```
Assets/StreamingAssets/
├── arkfx/            Windows 版（570 主包 + 51 依赖，311 MB，14665 prefab）
├── arkfx_android/    Android 版（570 主包 + 26 依赖，264 MB，14734 prefab）
└── filelist.txt      只服务安卓首启拷贝（固定排除 arkfx/，见下）
```

`ArkFxLoader.AbFolder` 编译期决定用哪个：

```csharp
public static string AbFolder
{
    get
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        return "arkfx_android";
#else
        return "arkfx";
#endif
    }
}
```

> ⚠️ 两套包的 **CAB 名完全相同**（同一份资源的不同平台构建），
> 放进同一个目录会互相覆盖，**必须分目录**，且运行时只加载其中一套。

### 安卓素材来源

安卓版来自对应版本客户端的 Android 解包：

```
D:\UnityWork\解包\effects_Android\     570 个 effects 包
D:\UnityWork\解包\Android\             依赖（扁平结构：[uc]shaders.ab / material.ab /
                                       sharedbattle.ab / shaders\ / texture\ / anon\）
```

验证结论（全量）：

| 项 | 结果 |
|---|---|
| 570 个包 | 全部 UnityFS format 8、引擎 **2021.3.39f1**（与工程同版本） |
| 压缩 | 全部 LZ4AK（`comp=3`），现有补丁可解 |
| `target_platform` | 全部 **Android** |
| 依赖 CAB | 需要 46 个，安卓依赖目录覆盖 **27 个**；核心 9 个（shaders/material/sharedbattle/texture×6）**全部齐全** |
| 未覆盖的 19 个 | 均为地图/建筑类边缘依赖，每个仅被 1~3 个包引用，不影响特效主体 |

### 生成命令

```bash
# Windows 版
python Tools/arkfx_merge.py --all --force

# Android 版
python Tools/arkfx_merge.py --all --force ^
  --src "D:\UnityWork\解包\effects_Android" ^
  --dep-root "D:\UnityWork\解包\Android" ^
  --out "Assets\StreamingAssets\arkfx_android" ^
  --series effects

# 参数导出（各自一份）
python Tools/dump_fx_params.py
python Tools/dump_fx_params.py --src "D:\UnityWork\解包\effects_Android" ^
  --manifest "Assets\StreamingAssets\arkfx_android\_manifest.json" ^
  --out "Assets\StreamingAssets\arkfx_android\fxparams.json"

# 生成 filelist（只影响安卓首启拷贝，固定排除 Windows 专用的 arkfx/）
python Tools/make_filelist.py
```

### 体积

| 目录 | 包数 | 体积 |
|---|---|---|
| `arkfx/` | 570 主包 + 51 依赖 | 311 MB（实测 316 MB） |
| `arkfx_android/` | 570 主包 + 26 依赖 | 264 MB |
| 合计 | — | **约 580 MB**（仓库已 gitignore，各自本地生成） |

### filelist 只服务安卓首启拷贝

```csharp
// Init.cs
#if UNITY_ANDROID
    await RunCopyOnFirstLaunch();     // 只有安卓会调
#endif
```
```csharp
// StreamingAssetsCopyUtility：桌面端直接跳过，连清单都不读
bool sameRoot = (AppHotfixResPath == streamingAssetsPath);
if (sameRoot) { MarkDone(); yield break; }
```

所以 **不需要分平台生成**：`make_filelist.py` 固定排除 Windows 专用的 `arkfx/`，
结果永远是安卓需要的那份（当前 **813 条，arkfx_android 600 条**）。

### ⚠️ 但 APK 体积是另一回事

`filelist.txt` 只控制**首启拷贝到 persistentDataPath 的内容**。
`StreamingAssets/` 下的**所有**文件仍会被打进 APK —— 包括 Windows 的 `arkfx/`（311 MB）。

| 目的 | 手段 |
|---|---|
| 减少首启拷贝 / 设备存储 | `make_filelist.py`（已排除 arkfx） |
| **减少 APK 体积** | 构建钩子 `ArkFxBuildHook`（自动移走另一平台目录，见 5.9） |

## 5.9 构建钩子：自动移走非当前平台素材（2026-10-28）

`Assets/Editor/ArkFxBuildHook.cs`

| 时机 | 行为 |
|---|---|
| `IPreprocessBuildWithReport` | 先把遗留状态恢复（上次构建异常中断的兜底），再把「非目标平台」那一套 `Directory.Move` 到 `Library/ArkFxBuildSwap/`，然后重新生成 `filelist.txt` |
| `IPostprocessBuildWithReport` | 无论构建成功/失败/取消，立刻移回原位并重生成 `filelist.txt` |
| `[InitializeOnLoadMethod]` | 编辑器启动时若存在 `Library/ArkFxBuildSwap/swapped.txt`，自动恢复（防止素材卡在临时目录里） |

- 移动的是**目录本身**（同盘 rename，瞬时），不是逐文件复制；实测 316 MB 目录移动 < 1 秒。
- `callbackOrder = 0`。
- 平台判定用 `report.summary.platform == BuildTarget.Android`：**Android → 移走 `arkfx/`；其它（含 Windows）→ 移走 `arkfx_android/`**。
- 如果目标平台要用的那套不存在，只 `LogWarning`，不阻断构建。

手动菜单：

| 菜单 | 用途 |
|---|---|
| `Tools/ArkFx/8. 手动移出非当前平台素材（演练用）` | 按 `EditorUserBuildSettings.activeBuildTarget` 移走另一套，可用来实测「打包后确实少 300 MB」 |
| `Tools/ArkFx/9. 恢复被移出的素材` | 手动恢复（正常情况不用，钩子会自己做） |

**实测验证（2026-10-28）**：手工模拟移动 `arkfx/`（316 MB）→ `Assets/StreamingAssets/` 只剩 `arkfx_android/` → 重新生成 filelist **仍为 813 条（arkfx_android=600）**，与移动前完全一致 → 移回后目录大小/包数不变。证明 filelist 生成规则与物理移动解耦，互不影响。

> 已知边界：只区分 Android / 非 Android。若将来要出 iOS 包，`ArkFxLoader.AbFolder`
> 与钩子的 keep/drop 判断都要再加分支（iOS 该用哪套素材目前无解，需要重新解 iOS 包）。
