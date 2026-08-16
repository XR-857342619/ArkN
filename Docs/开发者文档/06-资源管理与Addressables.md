# 06 资源管理与 Addressables

## 1. Addressables 配置

- 配置资产：`Assets/AddressableAssetsData/AddressableAssetSettings.asset`。
- 分组：`StandPic` 独立分组，其余资源进默认分组。
- 工具：菜单 **Tools → 重新标记**（`Assets/Editor/BuildEditor/BuildEditor.cs`）扫描 `PathHelper` 中定义的各资源目录，自动为资产创建/移动 Addressables 条目，并把地址设置为“无扩展名路径”。

## 2. PathHelper 资源路径约定

`Assets/Scripts/Helper/PathHelper.cs` 定义：

| 常量 | 路径 | 内容 |
|---|---|---|
| `DataPath` | `Assets/Bundles/Data/` | 配置 txt、地图 |
| `UIPath` | `Assets/Bundles/UI/` | FairyGUI 包与图集 |
| `SpritePath` | `Assets/Bundles/Image/` | 背景、头像 |
| `StandPicPath` | `Assets/Bundles/Image/StandPic/` | 干员立绘 |
| `UnitPath` | `Assets/Bundles/Units/` | 单位预制体 |
| `BulletPath` | `Assets/Bundles/Bullet/` | 弹道预制体 |
| `EffectPath` | `Assets/Bundles/Effect/` | 特效预制体 |
| `AudioPath` | `Assets/Bundles/Audio/` | BGM |
| `OtherPath` | `Assets/Bundles/Other/` | 地编网格、线条等 |
| `DungeonGridPath` | `Assets/Bundles/DungeonTile/` | 肉鸽地图块 |

## 3. 资源加载方式

### 3.1 Addressables

- UI 包：`Addressables.LoadAssetAsync<TextAsset>(PathHelper.UIPath + packageName + "_fui")`。
- 数据：`Addressables.LoadAssetAsync<TextAsset>(PathHelper.DataPath + name)`。
- 通用：`ResHelper.GetAsset<T>(path)`（`Assets/Scripts/Helper/ResHelper.cs`）与 `ResHelper.Prepare(index)` 预加载。

### 3.2 本地文件

- `SaveHelper.LoadFile(fileName)`：从 `PathHelper.AppHotfixResPath` 读取 UTF-8 文本。
- `SaveHelper.LoadMap(fileName)`：从 `MapResPath` 读取地图 JSON。
- `ResHelper` 中部分资源使用 `AssetBundle.LoadFromFile` 回退（旧路径）。

### 3.3 路径优先级

- 移动平台：`persistentDataPath/<productName>/`（热更）优先，其次 StreamingAssets。
- 编辑器：`Assets/Bundles/...` 直接通过 Addressables/AssetDatabase 加载。

## 4. Spine 资源

- 源文件：`Assets/Res/Spine/` 下按 `Enemy/`（敌人）、`Unit/`（干员/召唤物/陷阱）分类。
  - 单位：`Unit/char_<id>_<name>/front|back/`，每目录通常 6 个文件：`.atlas.txt`、`.png`、`.skel.bytes`（或 `.json`）等。
  - 敌人：`Enemy/enemy_<id>_<name>/`，每目录通常 8 个文件。
  - 召唤物/陷阱：`token_*`、`trap_*` 目录。
- `Assets/Res/SkillIcon/`：技能图标；`Assets/Res/头像/`：头像；`Assets/Res/遗物图标/`：肉鸽遗物图标。
- `Assets/Res/Shader/`：4 个 Shader（其中 `SpriteDistorted.shader` 原为 GBK，已转 UTF-8）。
- 运行时加载：
  - `SpineResourceManager`（`Assets/Scripts/SpineResourceManager.cs`）通过 Addressables Locator 收集所有 Spine 资源 Key；
  - `SpineImportHelper`（`Assets/Scripts/Helper/SpineImportHelper.cs`）从 `.atlas.txt + .png + .skel.bytes` 创建 `SkeletonDataAsset`，使用反射兼容 Spine 3.8 的二进制加载。
- 编辑器工具：菜单 **Tools → Spine移动信息**、**Tools → Spine转Prefab**（`Assets/Editor/SpineImportEditor.cs`）。

## 5. 资源打包与导出

- `Assets/Editor/BuildEditor/BuildEditor.cs` 提供 `Tools/重新标记`：按目录标记 Addressables。
- `Assets/Scripts/Tool/ABExportTool.cs`：AssetBundle 导出工具。
- `Assets/Editor/BuildEditor/EditorResHelper.cs`：编辑器资源路径收集。
- 资源导出/下载工具（`Assets/Scripts/Tool/`）：
  - `StandPicDownLoadTool`：从 PRTS Wiki 抓取干员立绘；
  - `SpineDownLoadTool` / `EnemySpineDownloadTool` / `TokenDownloadTool`：抓取 Spine 资源；
  - `HalfDownLoadTool`：抓取半身像；
  - `ResourcesExportTool`：资源导出（含场景模式）。

## 6. 资源开发注意

- 新增 `Assets/Bundles/` 下的资源后，需要执行 **Tools → 重新标记** 更新 Addressables 条目。
- `StandPic` 分组会读取 `UnitData.StandPic` 动态增删条目：只有被配置引用的立绘才会标记进 Addressables。
- 不要提交 `Assets/StreamingAssets/`（已被 .gitignore 忽略）。
- 资源路径大小写：Addressables 地址通常为无扩展名路径；`PathHelper` 中路径大小写要保持一致。
- 加载配置数据使用 `TextAsset.text`（要求 UTF-8）；历史 Excel 导出的 txt 由 `StreamWriter` 默认 UTF-8 写出，但部分代码文件曾为 GBK/UTF-16，本次已统一转为 UTF-8。
