# 23 · Unity 升级操作手册（2021.2.10f1 → 2021.3.39f1）

> **状态：闸门已验证通过（2026-10-26）**
> - 目标版本确定为 **Unity 2021.3.39f1（国际版）**，revision `fb3b7b32f191`
> - 编辑器安装路径：`D:\Program Files\Unity 2021.3.39f1\Editor\Unity.exe`
> - 闸门实测（空工程 probe）：`bundles_ok=101 shaders=196 supported=196 unsupported=0` ✅
> - 曾用中国版 `2021.3.39f1c1` 验证同样通过，但**正式采用国际版**（与现有工程同发行版）
>
> **升级已完成（2026-10-26）**：工程已跑在 2021.3.39f1 上，特效不再品红。
> 提交记录与 Spine 修复结论见文末附录 A / B。
>
> 目的：让方舟原版特效的 shader 能够运行。
> 依据：[22-ArkFx-P0P1验收手册 §15](22-ArkFx-P0P1验收手册.md) 的实测结论——
> 同一份 shader 包在 **2021.2.10f1 只有 2/197 可用**，在 **Unity 6000.0.40f1 是 197/197 可用**。
> 即：数据完好，**引擎太老**，不升级无法修复品红。

---

## 0. 一句话结论

**升级是唯一出路。** 但必须按「先验证、再动工程、后提交」的顺序做，因为：

- 工程 1.9 GB 资源 + 33 GB Library，一次全量重导入
- Spine 资源在升版时会被批量重导入，**GUID 会全变** → 场景/Addressables 引用悬空（上次对方升版踩过）
- 升版不可逆：Unity 2021.3 保存过的场景无法再用 2021.2 打开

---

## 1. 前置检查（不满足不要开始）

| 项 | 当前实测 | 要求 / 处理 |
|---|---|---|
| 目标编辑器 | 已装 `D:/Program Files/Unity 2021.3.39f1`（国际版，`fb3b7b32f191`） | 完成 |
| 磁盘 D: | 694 G 总 / **43 G 可用** | ⚠️ 危险。`Library` 占 **33 G**，升级前删掉可释放到 ~76 G |
| `Library` 构成 | BuildCache 21 G / Bee 6 G / Artifacts 4 G / addressables 1.8 G | 全部可再生，可安全删除 |
| 仓库 | `.git` 1.2 G，`Assets` 1.9 G | 建立独立分支 + 物理备份 |
| 许可证 | — | 确认 Unity Hub 已登录且有可用 license |
| 模块 | Windows Build Support (IL2CPP) 等 | 新编辑器勾选与 2021.2 一致的模块；如需安卓再勾 Android Build Support |

---

## 2. 阶段 0 · 安装编辑器

1. Unity Hub → 安装 → **2021.3.39f1**
2. 模块勾选：与当前 `D:\Program Files\2021.2.10f1` 保持一致（Windows Build Support、Android（若需要）、Documentation）
3. 安装路径建议 `D:\Program Files\2021.3.39f1`（与现有一致，便于切换）

> 版本号一定要 **`2021.3.39f1`**，与素材包的 `unity_version` 字符串完全一致。
> 若只能用 `2021.3.39f1c1`（中国版），先跑阶段 1 的闸门验证，能过再用。

---

## 3. 阶段 1（闸门）· 空工程验证 shader —— ✅ 已完成

**实测结果（2026-10-26，Unity 2021.3.39f1 国际版，工程 `D:\UnityWork\test4shader`）：**

```
PROBE_RESULT unity=2021.3.39f1 api=Direct3D11 bundles_ok=101 bundles_fail=0 shaders=196 supported=196 unsupported=0
PROBE_VERDICT 闸门通过：全部 shader 可用，可以开始升级工程
```

- 101 个重打包的 AB 在 2021.3 下全部可加载
- 196 个去重 shader **全部 `isSupported`**
- 对比：同一批文件在 2021.2.10f1 下只有 2/197 可用

> 探针脚本：`D:\UnityWork\test4shader\Assets\Editor\ShaderProbe.cs`
> 菜单 `Tools/ShaderProbe/检查方舟 shader 是否可用`，报告写到 `Logs/ArkFxShaderProbe.txt`

以下是原始闸门流程，留作以后换版本时复用：

1. 用 2021.3.39f1 新建一个空工程（3D / Built-in RP），路径用纯英文，如 `D:\ida_work\shaderprobe`
2. 在 `Assets/Editor/` 下新建 `ShaderProbe.cs`：

```csharp
using UnityEngine;
using UnityEditor;

public static class ShaderProbe
{
    public static void Run()
    {
        string p = @"D:/UnityWork/zhou-master/Assets/StreamingAssets/arkfx/_deps/1b70e77f_lz4.ab";
        var ab = AssetBundle.LoadFromFile(p);
        if (ab == null) { Debug.Log("PROBE_RESULT bundle_load_failed"); return; }
        var shaders = ab.LoadAllAssets<Shader>();
        int ok = 0;
        foreach (var s in shaders) if (s != null && s.isSupported) ok++;
        Debug.Log("PROBE_RESULT total=" + shaders.Length + " supported=" + ok
                  + " api=" + SystemInfo.graphicsDeviceType);
        int n = 0;
        foreach (var s in shaders)
            if (s != null && s.isSupported && n++ < 10) Debug.Log("PROBE_OK " + s.name);
    }
}
```

3. 菜单栏出现 `ShaderProbe`？没有的话用命令行跑：

```bat
"D:\Program Files\2021.3.39f1\Editor\Unity.exe" -batchmode -quit ^
  -projectPath "D:/ida_work/shaderprobe" ^
  -executeMethod ShaderProbe.Run ^
  -logFile "D:/ida_work/probe_run.log"
```

4. **判据**

| 结果 | 结论 | 下一步 |
|---|---|---|
| `total=197 supported=197` | ✅ 闸门通过 | 进入阶段 2 |
| `supported` 接近 197 但差几个 | ⚠️ 可接受 | 记录差异 shader，进入阶段 2 |
| `supported=2`（与 2021.2 相同） | ❌ 引擎版本不是主因 | **停止**，回来重新分析 |

---

## 4. 阶段 2 · 工程准备

### 4.1 提交当前状态

```bash
cd D:/UnityWork/zhou-master
git status                 # 先处理未提交的改动（ArkFx 相关文件都还没提交）
git add -A
git commit -m "chore: 升级 Unity 前的快照（含 ArkFx P0/P1）"
git checkout -b upgrade/unity-2021.3
```

### 4.2 物理备份（强烈建议）

升版不可逆，git 只能回滚**已提交**的内容，而 Unity 首次打开会**直接改写**工作区文件。

```bat
robocopy "D:\UnityWork\zhou-master" "E:\backup\zhou-master-before-2021.3" ^
  /MIR /XD Library Temp obj Logs .git /NFL /NDL /NP
```
（`/XD` 排除可再生的目录；`.git` 用 `git bundle` 单独备份更省空间）

```bash
git bundle create D:/backup/zhou-master.bundle --all
```

### 4.3 清理可再生目录（释放约 33 GB）

```bat
cd D:\UnityWork\zhou-master
rmdir /s /q Library
rmdir /s /q Temp
rmdir /s /q obj
rmdir /s /q Logs
```

> `Library/BuildCache`(21G) 是 Addressables 构建缓存，按 Unity 版本失效，删掉无损失。

### 4.4 记录基线（用于升级后对比）

```bash
python Tools/verify_arkfx_ab.py --ab-dir Assets/StreamingAssets/arkfx --header-only > D:/backup/arkfx_baseline.txt
```

---

## 5. 阶段 3 · 首次打开与自动升级

1. 用 **2021.3.39f1** 打开 `D:\UnityWork\zhou-master`
2. 弹出 **"Upgrade Project"** → 选 **Upgrade / Continue**
3. 弹出 **"API Update Required"** → 选 **Go Ahead (Recommended)**（Unity 会重写少量 API）
4. 等待首次导入（1.9 GB 资源 + 全家脚本重编译）——**预计 20~60 分钟**，硬盘灯会一直亮

### 检查点 3.1 · 版本号

```
ProjectSettings/ProjectVersion.txt  →  m_EditorVersion: 2021.3.39f1
```

### 检查点 3.2 · 编译

- Console **无红色 error**
- 菜单栏出现 `Tools/ArkFx`（说明 ArkFx 脚本编译通过）
- `Tools/Spine引用修复` 也在

### 检查点 3.3 · 通过闸门复验

在**当前工程**里跑一次 `Tools/ArkFx/4. 检查 AB 里的 Shader 是否可用`
→ 期望 `Shader 197 | 不支持 0`

> 做不到这一步就说明包/路径有问题，先别继续。

---

## 6. 阶段 4 · Spine GUID 修复（本次最大风险）

### 背景
上次对方升到 2021.3.39f1c1 时，Spine 资源被批量重导入：
**287 个 `Bundles/Units/*.prefab`、42 个 `Res/Spine/*.asset`、174 个 `.meta`、21 个 `.png` 的 GUID 全变**
→ 场景与 Addressables 里的引用悬空。

### 操作

1. 菜单 **`Tools/Spine引用修复/1-扫描错误引用(只报告)`**
   → Console 会列出所有引用错位的对象，**记下数量**
2. 选中报错的 prefab → **`Tools/Spine引用修复/2-修复选中的Prefab`**
   （若报错很多，按目录分批处理）
3. **再跑一次扫描**，直到数量为 0

### 判据
扫描结果 = 0，且场景里 Spine 模型能正常显示。

### 若工具本身失效
用 git 定位被改写的 meta：

```bash
git status --short | grep "\.meta$"
git diff --stat -- "*.meta" | tail -5
```
对**不应变化**的 `.meta` 执行 `git checkout -- <file>`，再让 Unity 重新导入；仍不行则回滚整个分支重来。

---

## 7. 阶段 5 · 验收清单

按顺序做，**前一项不过不要做下一项**。

### 5.1 资源与特效（核心目标）

| 检查 | 操作 | 期望 |
|---|---|---|
| Shader | `Tools/ArkFx/4` | `不支持 0` |
| 特效来源 | `Tools/ArkFx/0` 切到「原版 AB」，重进 Play | Console 出现 `[ArkFx] 命中 AB 特效: xxx` |
| 品红 | 放技能 | **不再品红** |
| UV 滚动 | 选中特效 → `Tools/ArkFx/2` 打印 Diag，隔 0.5 秒再看 | `_MainTex` 的 `offset` 在变 |
| UV 旋转 | 找 `fxparams.json` 里有 `"_Rotation"` 的 prefab | 画面有角度变化 |
| 组件统计 | `Tools/ArkFx/3` | `ArkFxEffectHost > 0`、`ArkFxUVTween (v5) = 0` |

### 5.2 玩法回归

- [ ] 进入战斗：部署 / 撤退 / 放技能 / 子弹 / 特效
- [ ] UI：主界面、干员页、DIY 编辑器
- [ ] 地图加载与地编
- [ ] 音效
- [ ] 存档读写

### 5.3 打包回归

- [ ] Addressables 重新构建（`Assets/Editor/BuildEditor`）
- [ ] 出 Windows 包，跑起来再验一遍 5.1
- [ ] （如需）Android 包构建

---

## 8. 阶段 6 · 提交策略

升版会产生**上万文件的机械变更**，务必分 3 个 commit，否则 review 无从下手：

| # | commit | 内容 |
|---|---|---|
| 1 | `chore: 升级 Unity 到 2021.3.39f1（配置文件）` | `ProjectVersion.txt`、`ProjectSettings/*.asset`、`Assets/*.asset`、`Packages/manifest.json`、`packages-lock.json` |
| 2 | `chore: 升版后资源重序列化（机械变更，无逻辑改动）` | 约 2937 个 `.mat` + 场景 + prefab |
| 3 | `fix: 修复升级导致的 Spine 资源 GUID 悬空` | `.meta` + 被修的 prefab |

> 提交前先 `git status --short | wc -l` 记一下规模，心里有数。

---

## 9. 回滚方案

| 情况 | 做法 |
|---|---|
| 还没提交、想放弃 | 用阶段 2 的物理备份整目录覆盖回来 |
| 已提交在分支上 | `git checkout master`（master 未被污染） |
| 想彻底退回 2021.2 | 还原备份 → 用 2021.2.10f1 打开（Library 会重建，再等一次导入） |

> ⚠️ **一旦 2021.3 保存过场景/prefab，该文件就无法再用 2021.2 打开**（序列化版本单向升级）。
> 所以：**只用独立分支 + 物理备份**，不要在主分支上做实验。

---

## 10. 常见问题排查

| 症状 | 原因 | 处理 |
|---|---|---|
| 弹 "API Update Required" | 正常现象 | 点 Go Ahead |
| 大面积 Missing Script | 脚本编译失败 / 命名空间变化 | 先看 Console 第一条 error；多半是第三方插件不兼容 |
| Spine 模型贴图丢失/引用悬空 | GUID 重导入 | `Tools/Spine引用修复` |
| Addressables 报 catalog / 资源找不到 | 升版后需重建 | 重新构建 Addressables |
| TMP 字体变方框 | TMP 资源版本变化 | `Window → TextMeshPro → ...` 重新生成 |
| 材质还是粉 | shader 仍不支持 | 回到阶段 5.1 跑 probe，看 `isSupported` |
| 编辑器打开极慢 / 卡死 | 磁盘不足 | 检查可用空间（Library 重建需要 ~35 G） |
| Astar / FairyGUI 报错 | 第三方插件不兼容 2021.3 | 升级对应插件版本 |

---

## 11. 时间线（预计）

| 阶段 | 内容 | 预计 |
|---|---|---|
| 0 | 装编辑器 | 0.5 h |
| 1 | **空工程 shader 闸门** | 0.5 h |
| 2 | 分支 + 备份 + 清理 | 0.5 h |
| 3 | 首次打开 + 导入 + 编译 | 1~2 h |
| 4 | Spine GUID 修复 | 2~4 h |
| 5 | 全量回归（玩法 + 打包） | 1~2 天 |
| 6 | 分 commit 提交 | 0.5 h |
| **合计** | | **约 2~3 天**（顺利的话），含不可预见问题留 1 周 |

---

## 12. 升级完成后的第一件事

回到 [22-ArkFx-P0P1验收手册 §2~§4](22-ArkFx-P0P1验收手册.md)：

1. `Tools/ArkFx/0` 切到「原版 AB」
2. 放技能，确认**不再品红**
3. `Tools/ArkFx/3` 确认 `ArkFxEffectHost > 0`、`ArkFxUVTweenV6 > 0`
4. 选带 UV 动画的特效（如 `kalts_skill_03_buff`）跑 Diag，确认 offset 在动

全部通过后，就可以继续 P2（`FxDelay`）/ P3（Emitters）了。

---

## 附录 A · 阶段 4 实测结论：⚠️ Spine 修复工具**不可批量使用**

### 实测数据（2026-10-26）

```
Tools/Spine引用修复/1-扫描错误引用(只报告)
→ 共 2440 个 prefab | 引用错位 475 个 | 跳过 29 个
```

看起来要修 475 个，实际逐条分析后：

| 分类 | 数量 | 说明 |
|---|---|---|
| 错位 prefab 总数 | 475 | |
| **全部字段名字相同 → 假阳性** | **468** | front/back 同名资产被误判 |
| 有字段名字不同 → 可能真错位 | 7 | 其中只有 5 个是真问题 |
| 字段级 null 引用 | 4 | 真的坏了 |

### 根因：工具按文件名匹配，遇到同名资产就随机挑一个

```
Assets/Res/Spine/Unit/char_002_amiya/back/char_002_amiya_SkeletonData.asset   (guid 150298a5…)
Assets/Res/Spine/Unit/char_002_amiya/front/char_002_amiya_SkeletonData.asset  (guid 03c6bd16…)
```

prefab 里两个 `SkeletonAnimation` **分别正确地**引用了 back 和 front。
工具的 `FindSkeletonData(model + "_SkeletonData")` 用 `AssetDatabase.FindAssets` 取第一个匹配，
于是把另一个正确引用判成"错位"——**执行修复会把 front/back 覆盖成同一个**。

### 正确做法

1. **绝不**在 `Assets/Bundles/Units` 上整体执行 `2-修复选中的Prefab`
2. 只对扫描报告里「当前值与期望值**名字不同**」或「当前值为 null」的条目，**逐个选中**再修
3. 修之前先确认该模型**没有**同名 front/back 资产（`find Assets/Res/Spine -name "<模型>_SkeletonData.asset" | wc -l`）

### 本次实际修了 5 个

| prefab | 问题 | 处理 |
|---|---|---|
| `char_4172_xingzh` | SkeletonData/Material 指向 `char_002_amiya_*` | ✅ 重建自身 Spine 资产并指回自己 |
| `enemy_1011_wizard` | MeshRenderer 材质为空 | ✅ 补回 |
| `enemy_1011_wizard_2` | 同上 | ✅ 补回 |
| `token_10027_ironmn_pile3` | 材质名带 `$0_` 前缀，且该模型有 3 套资产 | ⚠️ 未动，需人工确认 |
| `trap_012_mine` | `_Material-Multiply` vs `_Material` | ⚠️ 未动，多半是刻意的混合变体 |

> **结论：这次升级并没有破坏 Spine 引用**（没做批量重导入，只是让 Unity 原地升级），
> 真正需要修的只有 5 个历史遗留问题。

---

## 附录 B · 本次升级的提交记录

```
e10695ccc  fix(tools): ArkFx 菜单 4 改为自扫描加载，不再依赖 Play 模式
347a71a09  fix: 修正升版后暴露的 Spine 引用错位
70ec015c4  chore: 升版后资源重序列化（机械变更，无逻辑改动）
b9df493b2  chore: 升级 Unity 到 2021.3.39f1（版本与包配置）
```

变更规模：约 3785 个文件（其中 2937 个 `.mat` 为 serializedVersion 机械变更）。

**回滚点**：`D:\backup\backupzhou-master-20261026.bundle`（升级前的完整历史）
