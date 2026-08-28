# 06 资源管理与 Addressables

> 文档同步版本：**v1.6.5**（2026-08-28）。

## 1. Addressables 配置

- 配置资产：`Assets/AddressableAssetsData/AddressableAssetSettings.asset`。
- 当前分组：
  - `Built In Data`：内置资源；
  - `Common`：Spine Shader、影子贴图等少量公共资源（2 个条目）；
  - `Default Local Group`：主力本地资源组（当前 2920 个条目）；
  - `StandPic`：立绘独立分组（当前 108 个条目，按 `UnitData.StandPic` 动态增删）。
- 工具：菜单 **Tools → 重新标记**（`Assets/Editor/BuildEditor/BuildEditor.cs`）扫描 `PathHelper` 中定义的各资源目录，自动为资产创建/移动 Addressables 条目，并把地址设置为“无扩展名路径”。

## 2. PathHelper 资源路径约定

`Assets/Scripts/Helper/PathHelper.cs` 定义：

| 常量 | 路径 | 内容 |
|---|---|---|
| `DataPath` | `Assets/Bundles/Data/` | 配置 txt、地图 |
| `UIPath` | `Assets/Bundles/UI/` | FairyGUI 包与图集（36 个资源文件） |
| `SpritePath` | `Assets/Bundles/Image/` | 背景、头像（202 个资源文件） |
| `StandPicPath` | `Assets/Bundles/Image/StandPic/` | 干员立绘 |
| `UnitPath` | `Assets/Bundles/Units/` | 单位预制体（2348 个资源文件） |
| `BulletPath` | `Assets/Bundles/Bullet/` | 弹道预制体（当前仓库中该目录不存在，弹道仍主要走 `Units/Effect` 或运行时创建） |
| `EffectPath` | `Assets/Bundles/Effect/` | 特效预制体（414 个资源文件） |
| `AudioPath` | `Assets/Bundles/Audio/` | BGM（4 个资源文件） |
| `OtherPath` | `Assets/Bundles/Other/` | 地编网格、线条等（68 个资源文件） |
| `DungeonGridPath` | `Assets/Bundles/DungeonTile/` | 肉鸽地图块（7 个资源文件） |

## 3. 资源加载方式

### 3.1 Addressables

- UI 包：`Addressables.LoadAssetAsync<TextAsset>(PathHelper.UIPath + packageName + "_fui")`；`UIManager.LoadPackge` 失败时编辑器下回退 `AssetDatabase` 读 `_fui.bytes`。
- 数据：`Addressables.LoadAssetAsync<TextAsset>(PathHelper.DataPath + name)`（`Database.AddAsync` 的最终回退，address 不带扩展名）。
- 通用：`ResHelper.GetAsset<T>(path)`（`Assets/Scripts/Helper/ResHelper.cs`）与 `ResHelper.Prepare(index)` 预加载。

### 3.2 本地文件

- `SaveHelper.LoadFile(fileName)`：从 `PathHelper.AppHotfixResPath` 读取 UTF-8 文本。
- `SaveHelper.LoadBaseFile(fileName)`：Addressables 读取 `Assets/Bundles/Data/Map/Main/<fileName>`（地图优先路径）。
- `SaveHelper.LoadMap(fileName)`：从 `MapResPath` 读取地图 JSON；Android 上持久化路径缺失时才用 UnityWebRequest 回退 APK 内置 StreamingAssets。
- 热更配置数据：`Database.ResolveDataFiles` 优先扫描 `AppHotfixResPath/Data/<表名>/*.txt`，再由 `_index.txt` 定序。
- `ResHelper` 中部分资源使用 `AssetBundle.LoadFromFile` 回退（旧路径）。

### 3.3 路径优先级

- 移动平台：`persistentDataPath/<productName>/`（热更）优先，其次 StreamingAssets/Addressables。
- 编辑器：`Assets/Bundles/...` 直接通过 Addressables/AssetDatabase 加载。
- 首启复制：`StreamingAssetsCopyUtility` 按 `StreamingAssets/filelist.txt` 把初始资源复制到热更目录；`filelist.txt` 由编辑器菜单 `Tools → 生成StreamingAssets文件列表` 生成。

## 4. Spine 资源

- 源文件：`Assets/Res/Spine/` 下按 `Enemy/`（敌人，1876 个目录）、`Unit/`（干员/召唤物/陷阱，460 个目录）分类。
  - 单位：`Unit/char_<id>_<name>/front|back/`，每目录通常 6 个文件：`.atlas.txt`、`.png`、`.skel.bytes`（或 `.json`）等。
  - 敌人：`Enemy/enemy_<id>_<name>/`，每目录通常 8 个文件。
  - 召唤物/陷阱：`token_*`、`trap_*` 目录（另有根目录 `Assets/Res/trap/`）。
- 图标/头像类资源当前以 UI 包为主：技能图标 → `Bundles/UI/SkillIcon`，装备图标 → `Bundles/UI/EquipIcon`，通用图集 → `Bundles/UI/Res`；外部本地头像走热更目录 `Icon/`（`ExtextureLoader` 统一加载）。`Assets/Res` 下当前无独立的 `SkillIcon/`、`头像/`、`遗物图标/` 顶层目录。
- `Assets/Res/Shader/`：4 个 Shader（已全部 UTF-8）。
- 运行时加载：
  - `SpineResourceManager`（`Assets/Scripts/SpineResourceManager.cs`）通过 Addressables Locator 收集所有 Spine 资源 Key；
  - `SpineImportHelper`（`Assets/Scripts/Helper/SpineImportHelper.cs`）从 `.atlas.txt + .png + .skel.bytes` 创建 `SkeletonDataAsset`，使用反射兼容 Spine 3.8 的二进制加载；
  - `Unit.CreateModel` 先查 `SpineData`：命中时按 `SpineData.UseAppHotfixResPath` 选择热更路径或相对路径加载；未命中才回退 `ResHelper.Instantiate(UnitPath + Model)`。
- 编辑器工具：菜单 **Tools → Spine移动信息**、**Tools → Spine转Prefab**（`Assets/Editor/SpineImportEditor.cs`）。

## 5. 资源打包与导出

- `Assets/Editor/BuildEditor/BuildEditor.cs` 提供 `Tools/重新标记`：按目录标记 Addressables。
- `Assets/Editor/BuildFileList.cs` 提供 `Tools/生成StreamingAssets文件列表`。
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
- 加载配置数据使用 `TextAsset.text`（要求 UTF-8）。当前仓库 `.cs/.shader/.json/.txt/.md` 文本文件已全部通过 UTF-8 严格解码验证。
- 外部本地贴图（头像/纹理）统一由 `Helper/ExtextureLoader.cs` 处理，v1.6.2 起增加了缓存、失效保护与失败占位图；v1.6.5 后提供 `LoadTexture2D(path, onSuccess, onFailed)` 与 `TryGetCachedTexture2D(path, out texture)` 公共接口，其他脚本可直接获取带缓存的 `Texture2D`。
