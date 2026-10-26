# 22 · ArkFx P0/P1 验收手册

> 对应 [21-ArkFx特效系统改造规划](21-ArkFx特效系统改造规划.md) 的 P0（生命周期骨架）+ P1（UV 渲染层）。
> 本文档面向「在 Unity 编辑器里点几下就能确认改造是否生效」的场景。

---

## 0. 本次交付内容

### 新增文件

| 文件 | 阶段 | 作用 |
|---|---|---|
| `Assets/Scripts/Core/Effect/ArkFxBehaviour.cs` | P0 | 行为组件基类（OnPreload/OnPlay/OnFinish/OnPause/OnRecycle/IsBusy） |
| `Assets/Scripts/Core/Effect/ArkFxEffectHost.cs` | P0 | 生命周期宿主：统一驱动 + 结束判定 + 倍速/暂停/回池广播 |
| `Assets/Scripts/Core/Effect/ArkFxUVTweenV6.cs` | P1 | `FxUVTweener` 替身（×2 速度、mainTextureOffset、`<name>_ST`） |
| `Assets/Scripts/Core/Effect/ArkFxUVTweenAdvanceV6.cs` | P1 | `FxUVTweenerAdvance` 替身（tweenRepeat / tweenAnmTime / extraMaps） |
| `Assets/Scripts/Core/Effect/ArkFxUVRotation.cs` | P1 | `FxUVRotation` 替身（PropertyBlock + `_HG_UV_ROTATION` + `_Rotation0..3`） |
| `Assets/Editor/ArkFxDebugMenu.cs` | — | 验收菜单 `Tools/ArkFx/...` |
| `Tools/dump_fx_params.py` | P1 配套 | 从 AB typetree 生成 `fxparams.json` |

### 修改文件

| 文件 | 改动 | 影响 |
|---|---|---|
| `ArkFxLoader.cs` | 新增 `UseV6` 开关；`ApplyFxParams` 双轨挂载（v5/v6）；新增 `FxUVRotation` 分支；`Kick()` 改走 `ArkFxEffectHost` | 行为向后兼容，可一键回滚 |
| `Effect.cs` | `ResetEffect()` 里通知宿主 `Recycle()` | 修对象池复用不重播的隐患 |

### 生成的数据

```
Assets/StreamingAssets/arkfx/fxparams.json   （约 200 KB）
  → 205 个 prefab / 486 条参数
  → FxUVTweener 322、FxUVRotation 102、FxUVTweenerAdvance 62
```

---

## 1. 前置检查（编译 + 数据）

1. 打开 Unity（2021.2.10f1），等脚本编译完成 —— **Console 必须无编译错误**。
2. 确认菜单栏出现 **`Tools / ArkFx`**，下面有 3 项。
3. 确认 `Assets/StreamingAssets/arkfx/` 存在，且包含：
   ```
   effects/      76 个 *_lz4.ab
   _deps/        25 个 *_lz4.ab
   fxparams.json
   ```
4. 进入 Play，Console 应出现：
   ```
   [ArkFxLoader] 已加载 101 个包 | abDir=... | .ab 总数=101
   [ArkFxLoader] fxparams.json 载入 205 个 prefab 的原版脚本参数
   ```
   > 第二条是 P1 生效的关键标志。**没有这条 = fxparams.json 没读到**（路径或格式问题）。

---

## 2. 验收 A：生命周期骨架（P0）

**操作**
1. Play，进入一场战斗并放一个技能（让特效实例出现）。
2. 菜单 **`Tools/ArkFx/3. 统计场景里的 ArkFx 组件`**。

**期望输出**
```
[ArkFx] 场景组件统计
  ArkFxEffectHost      : >0        ← 宿主挂上了
  ArkFxUVTweenV6       : >0        ← v6 组件生效（不是 v5）
  ArkFxUVTweenAdvanceV6: 0 ~ >0
  ArkFxUVRotation      : 0 ~ >0
  ArkFxUVTween (v5)    : 0         ← 必须是 0（UseV6=true 时）
  ArkFxScriptStub      : >0        ← 未实现类的占位（正常）
  UseV6 开关           : True
```

**判据**
- `ArkFxEffectHost > 0` → P0 骨架生效
- `ArkFxUVTween (v5) == 0` → 确实走的是 v6
- 若 v5 > 0 → `UseV6` 被关掉了，用菜单第 1 项切回来并重新 Play

---

## 3. 验收 B：UV 滚动 v6（P1 核心）

### B-1 看材质 offset 是否在动

1. Play，放一个**带烟雾/流光**的技能（推荐 `aglina_skill_02_start`、`surtr_skill_01_start`）。
2. Hierarchy 里选中特效实例 → 菜单 **`Tools/ArkFx/2. 打印选中对象的 ArkFxDiag`**。
3. 在 Console（或 `Logs/ArkFxDiag.txt`）里看材质段：
   ```
   材质[0]: xxx | shader=Torappu/Particles/AlphaBlend
       tex _MainTex: smoke_01 512x512 wrap=Repeat scale=(1.0, 1.0) offset=(0.13, 0.71)
   ```
4. **间隔 0.5 秒再点一次**，`offset` 的数值应该在变。

### B-2 A/B 对比 v5 与 v6（确认差异方向）

| 步骤 | 操作 |
|---|---|
| 1 | 记录当前 v6 的 offset 变化速率（比如 1 秒涨 0.5） |
| 2 | 菜单 `Tools/ArkFx/1. 切换 UV 实现 v5 <-> v6` → Console 提示切到 v5 |
| 3 | **重新进 Play**（必须，已烘焙的 prefab 才会重新挂组件） |
| 4 | 同样的技能，再看 offset 变化速率 |

**期望**：v6 的速度大约是 v5 的 **2 倍**（原版 `Time.deltaTime * speed * 2f`）。
另外 v5 会往材质里写 `_UVTween` 向量（Diag 里会多一行 `vec _UVTween: (x,y,z,w)`），
v6 **不会有这行**（原版根本不写它）。

### B-3 看贴图 wrapMode

Diag 里 `_MainTex` 的 `wrap=` 应该是 **Repeat**。
`ArkFxUVTweenV6` 不会强制改 wrapMode（原版也不改），若发现是 Clamp 且 UV 滚出边界后变透明，
说明素材包里的贴图 import 设置有问题，不是代码问题。

---

## 4. 验收 C：UV 旋转（P1）

`FxUVRotation` 写的是 **MaterialPropertyBlock**，`ArkFxDiag` 只 dump 材质属性，看不到它。
所以用下面两种方式之一：

### 方式 1：视觉对比（推荐）
1. 找 `myrtle_attack_01_start_taiko#1`、`myrtle` 系列里带 `blow_circle` 节点的特效
   （fxparams.json 里 102 条 `FxUVRotation`，可用文本搜索 `"_Rotation"` 定位 prefab 名）。
2. Play 后观察该特效的圆环/光斑是否**有固定角度**（angle1=90）。
3. 临时把 `ArkFxUVRotation.angle1` 在 Inspector 里改成 `0`，对比是否明显不同。

### 方式 2：代码内打点（30 秒）
在 `ArkFxUVRotation.LateUpdate` 末尾临时加一行：
```csharp
if (Input.GetKeyDown(KeyCode.K))
    Debug.Log($"[ArkFxUVRotation] {name} R0={_mpb.GetVector(_rotationIds[0])} rot={_mpb.GetFloat(UvRotationId)}");
```
Play 后按 K，Console 应打印 `rot=1`、`R0=(0,-1,1,0)`（angle=90 时 cos=0, sin=1）。

---

## 5. 验收 D：对象池复用

1. 连续放**同一个技能 3 次**（间隔 1~2 秒）。
2. 期望：每次都正常播放；Console 无异常。
3. 反例（改造前会出现的 bug）：第二次开始特效不再播放 —— 因为宿主 `_played` 没复位。
   现在 `Effect.ResetEffect()` 会调 `host.Recycle()`，应当不再出现。

---

## 6. 验收 E：子特效/缺失依赖的健壮性

`fxparams.json` 里节点路径全部能命中（已用脚本校验 486/486）。
若在 Play 时看到：
```
[ArkFxLoader] 'xxx' 参数节点没找到: 'a/b/c' (FxUVTweener)
```
说明该 prefab 的层级在 AB 里和 dump 时不一致 —— 属异常，需要报告。

---

## 7. 已知限制（当前阶段必然存在的现象）

| 现象 | 原因 | 何时解决 |
|---|---|---|
| 部分特效 UV 仍然不动 | 该节点原本就没有 `FxUVTweener`（原始就是静态） | 正常 |
| 子特效（`OnPlayEmitter`）不出现 | P3 未实现 | 下一阶段 |
| 分层延迟播放的层次感缺失 | P2（`FxDelay`）未实现 | 下一阶段 |
| 左右/背向切换不正确 | `Switchable*` 未实现 | P4 之后 |
| 部分节点动得很奇怪 | 缺 `FxUVRotation` 以外的未实现类 | 后续阶段 |

---

## 8. 一键回滚

| 想回滚的部分 | 操作 |
|---|---|
| 只回滚 UV 实现 | 菜单 `Tools/ArkFx/1. 切换 UV 实现 v5 <-> v6`（或代码里 `ArkFxLoader.UseV6 = false`） |
| 回滚整个 P0/P1 | 删除 5 个新增运行时文件 + `ArkFxDebugMenu.cs`，并把 `ArkFxLoader.cs` / `Effect.cs` 用 git 还原 |
| 回滚数据 | 删除 `Assets/StreamingAssets/arkfx/fxparams.json`（Loader 会自动跳过） |

---

## 9. 排障对照表

| 症状 | 可能原因 | 处理 |
|---|---|---|
| Console 没有 `fxparams.json 载入 ...` | 文件不存在 / JSON 格式错 | 重跑 `python Tools/dump_fx_params.py` |
| `ArkFxUVTween (v5) > 0` | `UseV6` 为 false | 菜单切换后重新 Play |
| 统计里 `ArkFxEffectHost = 0` | 走的是工程内 Addressables 特效（不是 AB 特效） | 正常，只统计 AB 来源的特效 |
| 特效整体变成粉色 | shader 没加载 | 跑 `python Tools/verify_arkfx_ab.py --ab-dir Assets/StreamingAssets/arkfx --header-only` |
| 粒子完全不播 | `Kick()` 没被调用 | 检查 `EffectManager.GetEffect` 是否走了 `fromAb` 分支 |

---

## 10. 故障修复记录：容器格式不兼容（已修）

### 现象
进 Play 后 Console 刷出 303 条错误：
```
Unable to read header from archive file: .../arkfx/_deps/0d0c905b_lz4.ab
Failed to read data for the AssetBundle 'StreamingAssets\arkfx\_deps\0d0c905b_lz4.ab'.
[ArkFxLoader] 包加载失败: ...
```

### 根因
`UnityPy` 的 `save(packer="lz4")` 写出的是：

| 字段 | UnityPy 写出 | Unity 2021.2 要求 |
|---|---|---|
| 容器格式版本 | **8** | **7** |
| blocksInfo 位置 | 文件尾（flags 0x80） | 头部之后、内联（flags 0x40） |
| 头部对齐 | 无 | **补零到 16 字节边界** |
| 块压缩 | LZ4 (2) | LZ4HC (3)，flags = 0x43 |

> 序列化文件本身没问题：两者 `header.version` 都是 **22**，数据是兼容的，只是**外壳**不被 2021.2 接受。

### 修复
在 `Tools/arkfx_merge.py` 增加 `write_unityfs()`，按 Unity 2021.2 的真实布局手写容器：
- 固定 `bundle_format = 7`
- `flags = 0x43`（blocksInfo 内联 + LZ4HC）
- 头部按 16 字节补零
- SerializedFile 字节原样搬运（不重新序列化）

修复后头部与 Unity 2021.2 自己产出的 AB 逐字节同构：
```
UnityFS\0 | 00000007 | "5.x.x"\0 | "2021.2.10f1"\0 | size | cinfo | uinfo | 00000043 | 0000...
```

### 复现命令
```bash
python Tools/arkfx_merge.py --force          # 全量重打（37 秒）
python Tools/verify_arkfx_ab.py --ab-dir Assets/StreamingAssets/arkfx --header-only
# 期望：构建引擎版本 {'2021.2.10f1': 101} / 数据块 flag=3 (LZ4HC) / 判定 ✓
```

---

## 11. 故障修复记录：看不到 fxparams 日志 / AB 特效从不生效（已修）

### 现象
```
[ArkFxLoader] 已加载 101 个包 | ...
```
之后**没有** `fxparams.json 载入 ...`，也看不到任何原版特效。

### 根因（两条，叠加）

**① `LoadParams()` 调用时机太晚**
它只挂在 `ApplyFxParams()` 里，也就是「第一个 AB 特效真正生成」时才触发。
所以启动时看不到这条日志是必然的。

**② 表驱动特效根本走不到 AB 分支**（更关键）
`EffectManager.GetEffect()` 的逻辑是：

```csharp
var go = ResHelper.GetAsset<GameObject>(PathHelper.EffectPath + config.Prefab);  // 先找工程内
if (go == null) { go = ArkFxLoader.LoadPrefabByName(config.Prefab); ... }         // 找不到才回落 AB
```

实测：`Assets/Bundles/Effect/` 里有 **414 个 prefab**，而 `EffectData` 表里 343 个特效名
**全部都能命中**（`BulletData` 57 个模型同理，缺失 0 个）。
→ `ResHelper.GetAsset` 永远成功，**`ArkFxLoader.LoadPrefabByName` 一次都不会被调用**。

### 修复

| 改动 | 文件 | 说明 |
|---|---|---|
| `LoadParams()` 提前到 `LoadAll()` 末尾 | `ArkFxLoader.cs` | 启动即可确认 fxparams 是否可用；汇总日志新增 `fxparams=N 个 prefab` 字段 |
| 找不到 / 解析失败时打 Warning | `ArkFxLoader.cs` | 不再是静默返回 |
| 新增 `ArkFxLoader.ForceAbSource` 开关 | `ArkFxLoader.cs` | 强制跳过工程内 prefab，走原版 AB |
| 尊重该开关 | `EffectManager.cs` / `BulletManager.cs` | `ForceAbSource ? null : ResHelper.GetAsset(...)` |
| 菜单新增第 0 项 | `ArkFxDebugMenu.cs` | `Tools/ArkFx/0. 切换 特效来源 工程内 <-> 原版AB` |

### 现在的验收流程（更新版）

1. 进 Play，Console 应看到：
   ```
   [ArkFxLoader] fxparams.json 载入 205 个 prefab 的原版脚本参数
   [ArkFxLoader] 已加载 101 个包 | ... | fxparams=205 个 prefab
   ```
2. 菜单 **`Tools/ArkFx/0. 切换 特效来源 工程内 <-> 原版AB`** → 切到「原版 AB」
3. **重新进 Play**（`EffectManager.effectPrefabs` 有按 id 的缓存，热切不生效）
4. 放技能 → 这时才会真正实例化原版特效，`fromAb = true`
5. 接着做 §2/§3/§4 的验收（组件统计、ArkFxDiag、对象池复用）

> 注意：切到「原版 AB」后，画面上的特效会从「你们手搓的版本」变成「方舟原版」，
> 两者观感本来就不一样——这正是 A/B 对比的目的，不要误判为 bug。

### 菜单一览（更新）

| 序号 | 菜单 | 作用 |
|---|---|---|
| 0 | 切换 特效来源 工程内 <-> 原版AB | **验收 AB 特效必须先打开** |
| 1 | 切换 UV 实现 v5 <-> v6 | A/B 对比 UV 语义 |
| 2 | 打印选中对象的 ArkFxDiag | 导出材质/贴图/粒子报告 |
| 3 | 统计场景里的 ArkFx 组件 | 快速确认宿主与 v6 组件已挂上 |

---

## 12. 补丁：验收开关点不动 / 重进 Play 失效

### 现象
菜单切换「特效来源」后统计仍显示 `工程内 Addressables`，`ArkFxEffectHost = 0`。

### 根因
1. `[MenuItem(..., true)]` 的 Validate 里写了 `return Application.isPlaying` —— **Play 模式下才有勾选状态，Play 之外菜单是灰的、点了没反应**
2. `ForceAbSource` / `UseV6` 是普通 `static` 字段，Unity 每次进 Play 都会做 Domain Reload，**值被重置回默认**

### 修复
- 两个开关改为属性 + **PlayerPrefs 持久化**（键：`ArkFx.ForceAbSource` / `ArkFx.UseV6`）
- 新增 `[RuntimeInitializeOnLoadMethod(SubsystemRegistration)] RestoreDebugSwitches()`，
  在 `ArkFxBootstrap`（BeforeSceneLoad）**之前**恢复开关值
- 取消菜单的 `isPlaying` 校验，编辑器里随时可点
- `LoadPrefabByName` 命中 AB 时打日志：`[ArkFx] 命中 AB 特效: xxx（来源包 yyy）`

### 与 Addressables Play Mode Script 无关
`ResHelper.GetAsset<T>(path)` 在三种 Play Mode Script（Use Asset Database / Simulate Groups / Use Existing Build）
下**都会成功**返回工程内的 prefab —— 因为它本来就注册成了 Addressable。
所以问题不在 Addressables，而在 `EffectManager` 的**优先级**：先找工程内、找不到才回落 AB。
工程内 414 个 prefab 覆盖了表里全部 343 个名字，AB 分支永远走不到。

### 已知边界
打开 `ForceAbSource` 后，`EffectData` 里 18 个**工程内自建名**（如"蓄力""击中1""buff_cold1"）
在 AB 里不存在 → `LoadPrefabByName` 返回 null → 会打 `所有 arkfx 包里都没找到 prefab` 并**不生成该特效**。
如需保留这些特效，应在 P5（EffectData 加 `Source` 列）里逐条指定来源，而不是全局强制。

---

## 13. 故障排查：AB 特效全部品红（紫）

### 判读
**品红 = `Hidden/InternalErrorShader`**，只有两种成因：
1. 材质的 shader 引用**没解析到**（PPtr 为空）
2. shader 拿到了但 **`isSupported == false`**（编译数据不可用）

**贴图丢失不会变品红**（会变黑 / 白 / 灰）。所以可以排除贴图问题。

### 已排除的两项

| 怀疑点 | 实测 | 结论 |
|---|---|---|
| 我们的重打包损坏了 shader 数据 | `[uc]shaders.ab` 重打前后 SerializedFile **md5 完全一致**（`f57ea676956a`，1368016 字节） | 排除 |
| 图形 API 不匹配 | `ProjectSettings` 里 Windows 未单独配置 → 默认 **D3D11**，与包的目标平台一致 | 排除 |

### 最可能的原因
`[uc]shaders.ab` 的 shader 是 **Unity 2021.3.39f1** 编译的（`m_ShaderIsBaked = true`，编译产物已烘焙进包），
而工程是 **2021.2.10f1**。Unity 的 shader 编译数据跨版本不保证兼容 → `isSupported == false` → 品红。

### 实测结果（2026-10-25）

```
[ArkFx] 已加载 bundle 101 个；Shader 总数 197，不支持 195，空 0
```

`[uc]shaders.ab` 里 197 个 shader 中：**195 个 `isSupported=false`**，仅 2 个可用。
其中 197 个里 189 个是 `Torappu/*`，另外 8 个也是自定义（`Hidden/Torappu/*`、`UI/HomeBlur`…），
**没有一个是 Unity 内置 shader** —— 说明不存在"内置 shader 恰好能用"的解释。

→ **Unity 版本不兼容确诊**（shader 编译产物由 2021.3.39f1 生成，工程为 2021.2.10f1）。

### 确诊方法（编辑器内两条菜单）

| 菜单 | 看什么 | 判读 |
|---|---|---|
| `Tools/ArkFx/4. 检查 AB 里的 Shader 是否可用` | `Shader 总数 / 不支持数` | `不支持 == 总数` → **版本不兼容确诊** |
| `Tools/ArkFx/5. 打印选中对象每个材质的 shader 状态` | `shader=xxx` 与 `InternalErrorShader（没解析到）` | 若显示 `InternalErrorShader` → 是**依赖解析**问题（另一类 bug） |

### 如果是版本不兼容，可选路线

| 路线 | 说明 | 代价 |
|---|---|---|
| **A. 工程升到 2021.3.39f1** | 与素材包同版本，根治 | 需重过之前回滚的 2937 个 `.mat` / ProjectSettings，工作量大 |
| **B. 找 2021.2 时期构建的方舟素材** | 若手上有旧版客户端 APK | 不确定存在，且特效集是旧的 |
| **C. 运行时替换 shader** | 把 Torappu shader 换成工程内的近似 shader | 保真度大降：`_UVTween` / `_Rotation` / Disturb / Dissolve 全部失效，P1 的逆向成果大部分用不上 |
| **D. 还原 shader 源码** | 从 GPU 字节码反推 ShaderLab | 不现实 |

> 结论：**A 是唯一能保住"还原原版特效"目标的路线**。B/C/D 都会牺牲核心目标。

---

## 14. 升级 Unity 2021.3.39f1 的迁移成本评估

### 前置：先做 30 分钟的"最小验证"，不要直接升工程

升级整包工程是大动作。**先用一个空工程验证 shader 能不能跑**：

1. Unity Hub 装 **2021.3.39f1**（或与素材同版本）
2. 新建空工程（Built-in RP）
3. 把 `_deps/1b70e77f_lz4.ab`（shader 包）拷进去
4. 一个 3 行的 Editor 脚本：`AssetBundle.LoadFromFile(...).LoadAllAssets<Shader>()`，打印 `isSupported`
5. **期望**：195 个变 OK → 升级有意义
   **若仍是 FAIL**：说明方舟用的是**魔改 Unity**（已知它改过 LZ4AK 压缩），stock Unity 任何版本都跑不了，
   需要转 C 路线（替换 shader）

> 这一步先花 30 分钟，能避免几天的白工。

### 若最小验证通过，工程侧的实际改动量

| 项 | 内容 | 风险 |
|---|---|---|
| Unity 版本 | `ProjectSettings/ProjectVersion.txt` → 2021.3.39f1 | 低 |
| ProjectSettings 系列 | `ProjectSettings.asset` / `QualitySettings.asset` / `PackageManagerSettings.asset` / `Packages/manifest.json` / `packages-lock.json` 会被自动重序列化 | 中（需要复核 Quality/图形设置） |
| 材质批量重序列化 | 约 **2937 个 `.mat`** 仅 `serializedVersion` 与关键字字段变化 | 低（纯噪音，但 git diff 巨大） |
| 场景 | 2 个 `.unity` | 低 |
| **Spine 资源重导入** | 287 个 `Bundles/Units/*.prefab`、42 个 `Res/Spine/*.asset`、174 个 `.meta`、21 个 `.png` 的 **GUID 全变** → 场景/Addressables 引用悬空 | **高**（PR 作者提供了 `SpinePrefabReferenceFixer` 工具兜底：`Tools/Spine引用修复`） |
| 第三方插件 | Astar Pathfinding、FairyGUI、Spine runtime 3.8 | 中（需逐个验证编译与运行） |
| Android/打包流程 | 之前为 Android 做的 Addressables 配置需要重跑一次构建 | 中 |

### 时间估算

| 阶段 | 时间 |
|---|---|
| 最小验证（空工程试 shader） | 0.5 天 |
| 工程升级 + 首次导入 + 修 GUID | 1~2 天 |
| 全量回归（场景 / UI / 战斗 / 打包） | 2~3 天 |
| **合计** | **约 1 周**（含不可预见问题） |

### 升级前的决策要点

1. **是否必须保留 Torappu shader？**
   如果答案是"是"（要做原版还原），升级是唯一路。
   如果可以接受"用工程内 shader 近似"，则不必升级，但要放弃 `_UVTween` / `_Rotation` / Disturb / Dissolve 等特性，
   且 P0/P1 的逆向成果大部分作废。
2. **升级是不是只为了特效？**
   如果还有别的理由（新 API、插件要求），那升级的账要合并算。
3. **能不能只升一个分支？**
   建议在独立分支上做，主线保持 2021.2，验证通过后再合并。

---

## 15. 品红问题最终定性（有决定性实验证据）

### 实验：用 Unity 6000.0.40f1 加载同一个 shader 包

在同一台机器上用 batchmode 建了个空工程，加载**我们重打包出的**
`Assets/StreamingAssets/arkfx/_deps/1b70e77f_lz4.ab`：

```
Unity 2021.2.10f1  →  PROBE_RESULT total=197 supported=2      (195 个不支持)
Unity 6000.0.40f1  →  PROBE_RESULT total=197 supported=197    (全部支持)
```

### 结论

| 结论 | 证据 |
|---|---|
| shader 数据**完好无损** | 2022+ 引擎能 197/197 全部加载并支持 |
| 我们的重打包**没有破坏任何东西** | 重打前后 SerializedFile md5 一致；且在现代引擎里 197/197 可用 |
| **唯一原因是 Unity 版本太老** | 同一份文件在 2021.2 只有 2 个可用，在 6000.0 全部可用 |

> 补充：2021.2 下能用的那 2 个（`Torappu/PostEffect/Blend-Blit`、`Torappu/UI/Mesh/UnlitWaving`）
> 也是 Torappu 自家 shader，不是 Unity 内置 —— 说明不是"内置 shader 恰好能用"，
> 而是那两个 shader 的字节码恰好落在 2021.2 能接受的形式内。

### 因此，ArkFx 方案要落地，**必须升级 Unity**

- 目标版本建议 **2021.3.39f1**（与素材包构建版本一致，也是 PR 作者的环境）
- 迁移成本见 §14（约 1 周，最大风险是 Spine 资源 GUID 重导入）
- 不升级的唯一替代路线是"替换 shader"，但那会让 `_UVTween` / `_Rotation` / Disturb / Dissolve
  等特性全部失效，等于放弃"还原原版特效"这个目标

---

## 16. P1 · UV 实现验收结果（2026-10-26，Unity 2021.3.39f1）

### 验收方法

用菜单 **`Tools/ArkFx/6. 采样选中特效的 UV 偏移（约 2 秒）`**：
每帧采样所有 Renderer 的 `_MainTex` offset，输出取值范围、首末值、是否越界/回绕。

### 已验证项

| # | 验证点 | 目标（prefab / 节点） | 原版参数 | 期望 | 实测 | 结论 |
|---|---|---|---|---|---|---|
| 1 | **×2 速度系数** | `aglina_skill_02_start` / `huan` | `xspeed=-0.5` | -1.0 /秒 | **≈1/秒** | ✅ |
| 2 | **主贴图走 `mainTextureOffset`** | 同上 | — | `_MainTex_ST` 在变、无 `_UVTween` 写入 | 相符 | ✅ |
| 3 | **`keepInitOffset`** | 同上 | — | y 保持材质烘焙值 `-0.88` 不变 | 相符 | ✅ |
| 4 | **`protectMainUV` 回绕** | `aglina_skill_03_start_summer#5` / `lanliu` | `yspeed=-0.5, protect=true` | offset 被包裹在 `[0,1)` 内循环 | **符合预期** | ✅ |

对照项：`huan`（`protect=false`）的 offset 一路减到负值不回绕 —— 与 `lanliu` 形成正确反差。

### 待验证项

| # | 验证点 | 目标 | 判据 |
|---|---|---|---|
| 5 | 第二贴图 `<name>_ST` 向量 | `kalts_skill_03_buff` / `canying1`（`_DisturTex`，`secondXSpeed=0.2, secondYSpeed=-0.3`） | Diag 里 `_DisturTex_ST` 的 **z/w**（offset）按 +0.4 / -0.6 每秒变化，x/y（scale）不变 |
| 6 | UV 旋转矩阵 | 任一含 `FxUVRotation` 的 prefab | Diag 里 `_Rotation0=(0,-1,1,0)`（angle=90° 时 `(cos,-sin,sin,cos)`），未启用的为 `(1,0,0,1)` |

### 过程中修掉的实现缺陷

| 缺陷 | 影响 | 修复 |
|---|---|---|
| `ApplyMissingScriptFallback` 判重没检查 v6 组件 | 每次 `Kick`（含对象池复用）重复 `AddComponent<ArkFxUVTweenV6>`，**UV 速度按次叠加** | 改为任一 ArkFx 替身组件存在即跳过 |
| `ArkFxDiag` 用了 `:S2` 非法格式符 | Unity 2021.3 下抛 `FormatException`，诊断器完全不可用 | 改为 `:F2` / `:F1` |
| `ArkFxDiag` 不打印 ArkFx 组件 | 无法验收 UV | 新增 ArkFx 组件段 + 材质当前生效 ST / 旋转矩阵 |

---

## 17. P2 · FxDelay 验收结果（2026-10-26）

> **结论：FxDelay + ArkFxTicker 实测通过。**

### 实现要点（三处必须的偏离）

| # | 原版做法 | 为什么不能照抄 | 我们的做法 |
|---|---|---|---|
| 1 | `OnEnable` 里 `SetActive(false)` | `BakePrefab` 会把组件加到 AB 的 prefab **资产**上，在 OnEnable 里禁用会永久禁用资产节点 | 改由宿主 `OnPlay()` 上膛 |
| 2 | `Invoke("_DelayFunc", t)` | 节点隐藏后自身的 Invoke/协程不再推进 | 改由 `ArkFxTicker`（`DontDestroyOnLoad` 全局时钟）驱动 |
| 3 | 隐藏自己所在的 GameObject | **144/145 个 FxDelay 挂在特效根节点**，隐藏根会让 `ArkFxEffectHost.Update` 一起停掉 | 计时放在宿主之外 |

### 验收中发现并修复的两个叠加 bug

| bug | 现象 | 修复 |
|---|---|---|
| `EffectData.LifeTime` 无默认值（89/369 行没填） | 特效一生成就回池，只活一帧 | 字段加默认值 `5f` |
| `GetEffect` 先 `Kick` 后 `ResetEffect` | `ResetEffect → host.Recycle()` 把 FxDelay 刚隐藏的根节点又激活，延迟被抵消 | 改为先 `ResetEffect` 再 `Kick` |

### 遗留事项（计划在 P3 一并处理）

- `ArkFxDelay.OnRecycle()` 里的"强制激活"假设了调用方一定是回池场景；
  若将来有别的路径在对象存活时调 `Recycle()`，会误触发激活。
- 拟改为显式传参或拆分接口。

---

## 18. P3 · Emitters 验收结果（2026-10-26）

> **结论：4 类发射器实测通过**（`agoat2_skill_03_buff_01` → 5 个子特效正常生成）。

### 验收输出

```
[ArkFx] ArkFxOnPlayEmitter enabled=True playbackSpeed=1
    effects=agoat2_skill_03_weapon_01,...,05 instant=False preDelay=0.00 clearWhenFinish=True
    已发射子特效=5  waitingActive=False
```

Hierarchy 中出现 5 个独立的 `agoat2_skill_03_weapon_0X(Clone)` 对象。

### 验收中发现并修复的严重 bug（P1/P2 就已存在）

**现象**：第一次释放特效子特效正常，**第二次起 `已发射子特效=0`**，粒子也不播。

**根因**：`EffectManager.GetEffect` 里的 `fromAb` 是**局部变量**，只在"首次加载 prefab"分支被置 true。
prefab 进 `effectPrefabs` 缓存后，第二次起不再走加载分支 → `fromAb` 恒为 false →
`ArkFxLoader.Kick()` 永不调用 → `ArkFxEffectHost.Play()` 不执行 →
**所有 ArkFx 组件（发射器 / FxDelay / 粒子）都收不到 OnPlay**。

**修复**：改用 **prefab 上是否存在 `ArkFxEffectHost`** 判定 AB 来源
（`BakePrefab` 会给 AB prefab 挂宿主，工程内 prefab 没有），
`BulletManager.Get` 同步修复。

### ⚠️ 新增回归用例（必须做）

> **连放两次同一个技能，第二次的特效行为必须与第一次一致。**

因为 `EffectManager.effectPrefabs` / `BulletManager.bulletPrefabs` / `Pool<T>` 三层都有缓存，
"只在第一次正确"这类 bug 非常隐蔽。此后所有 ArkFx 验收都要覆盖：
- 第 1 次释放：粒子播 / 子特效发射 / 延迟生效
- **第 2 次释放：同上（关键）**
- 对象池复用（连放 3 次以上）

### 遗留

- `OnPlayEmitterIf` 的 buff / 朝向条件判定尚未实现，当前按无条件发射处理（仅 2 个实例）。
