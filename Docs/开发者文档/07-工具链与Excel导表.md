# 07 工具链与 Excel 导表

> 文档同步版本：**v1.6.5**（2026-08-28）。

## 1. Excel 导表

### 1.1 表结构约定

Excel 文件放在 `Excel/<作者或分类>/`（如 `Main/`、`Test/`、`一方通行/`、`尊尼获加/`、`Diy优胜/`）。

每个 Sheet 对应一个配置类，格式如下：

| 行号 | 含义 |
|---|---|
| 第 1 行 | 字段中文名/说明 |
| 第 2 行 | 字段名（必须与 `Config/*.cs` 属性名一致） |
| 第 3 行 | 字段类型 |
| 第 4 行起 | 数据，首列 Id，`#` 开头为注释 |

### 1.2 支持的类型

| 类型 | 输出 JSON |
|---|---|
| `int` / `int32` / `int64` / `long` / `float` / `double` | 数字 |
| `bool` | `true/false` |
| `string` | `"字符串"` |
| `string[]` | `["a","b"]`（先按 `,` 分，失败按换行分） |
| `int[]` / `object[]` | `[1,2]` |
| `XxxEnum` | 枚举转整数 |
| `XxxEnum[]` | 枚举数组转整数数组 |
| `其他表名`（如 `UnitData`、`SkillData`） | 该表 Id 所在**行下标**（int） |
| `其他表名[]` | 整数数组 |
| `UnityEngine.Vector2` / `Vector2Int` | `{"x":1,"y":2}` |
| `UnityEngine.Vector3` / `Vector3Int` | `{"x":1,"y":2,"z":3}` |
| `UnityEngine.Vector2[]` / `Vector2Int[]` | `[{"x":1,"y":2},...]`，点之间用 `#` 分隔 |
| `UnityEngine.Rect` | `{"x":1,"y":2,"width":3,"height":4}` |
| `Data` | 直接内嵌 `{...}` |

### 1.3 导出方式

1. **编辑器菜单导出**：`Tools → 导出配置`（`Assets/Editor/ExcelEditor/ExcelExportEditor.cs`）
   - 输入目录：`./Excel` 下**所有** `.xlsx`（递归），跳过 `$` 临时文件与备份目录；
   - 生成配置类：`Assets/Scripts/Config/<SheetName>.cs`（注意：会覆盖）；
   - 合并所有同名 Sheet 并导出到 `Assets/Bundles/Data/<SheetName>.txt`（每行一个 JSON）；`UnitData` 首行写入 `Id=0` 占位行；
   - 自动 `AssetDatabase.Refresh()`。
2. **游戏内导出**：主界面勾选玩家 Excel → 点击导出，走 `ExcelHelper.Export(ExcelList)`（`Assets/Scripts/Helper/ExcelHelper.cs`）。
   - 用于玩家自定义表（DIY/一方通行/尊尼获加等），运行时把选中的 Excel 导出到热更路径；
   - 并行写出 `Data/<SheetName>/<Excel文件名>.txt`，并为每个 Sheet 生成 `Data/<SheetName>/_index.txt`；
   - 导出后 `Database.Clear()` → `Database.Init()` 重新加载，并 `GameData.RefreshCardData()`。

### 1.4 导出实现要点

- `ExportClass()` 第一遍扫描所有 Sheet，建立 `表名 → Id 列表`，用于把引用其他表的字段转成下标。
- 编辑器 `ExportData()` 逐 Sheet 合并生成 JSON 行，写到 `Data/<SheetName>.txt`。
- 游戏内 `ExportData()` 使用 `Parallel.ForEach` 按 Excel 文件并行导出，再由主线程统一生成 `_index.txt`。
- `Convert(type, value)` 是类型转换核心，注意：
  - `string[]` 分隔失败时用 `\n`；
  - `Vector2[]` 分量之间用 `,`，点之间用 `#`；
  - 引用表类型找不到 Id 会抛异常并 `TipManager.ShowTip("导表错误:...")`。
- 运行时导出的分类 txt 由 `Database.AddAsync` 读取：优先 `Data/<表名>/` 分类目录 + `_index.txt` 定序，缺失时回退旧版单文件 `Data/<表名>.txt` / Addressables。

## 2. 编辑器菜单

| 菜单 | 脚本 | 功能 |
|---|---|---|
| `Tools/导出配置` | `Assets/Editor/ExcelEditor/ExcelExportEditor.cs` | 从 `Excel/` 全部 xlsx 导表到 `Assets/Bundles/Data` 并生成 Config 类 |
| `Tools/Excel列同步/选择基准文件并同步` | `Assets/Editor/ExcelEditor/ExcelColumnSyncTool.cs` | 以基准 Excel 同步目标 Excel 的 Sheet 列结构，同步前备份 |
| `Tools/Excel列同步/同步指定Sheet列` | `Assets/Editor/ExcelEditor/ExcelColumnSyncWindow.cs` | 窗口式指定 Sheet 列同步 |
| `Tools/重新标记` | `Assets/Editor/BuildEditor/BuildEditor.cs` | 重新标记 Addressables |
| `Tools/生成StreamingAssets文件列表` | `Assets/Editor/BuildFileList.cs` | 生成 `StreamingAssets/filelist.txt` |
| `Tools/Spine移动信息` | `Assets/Editor/SpineImportEditor.cs` | Spine 移动信息处理 |
| `Tools/Spine转Prefab` | `Assets/Editor/SpineImportEditor.cs` | Spine 转 Prefab |
| `GameObject/FairyGUI/...` | `Assets/Editor/FairyGUI/EditorToolSet.cs` | 创建 FairyGUI UI Panel/Camera |
| `Window/FairyGUI - Refresh Packages And Panels` | `Assets/Editor/FairyGUI/EditorToolSet.cs` | 刷新 FairyGUI 包 |

## 3. 运行时工具（Assets/Scripts/Tool）

| 工具 | 功能 |
|---|---|
| `ExcelHelper` | Excel 读写、并行导出、生成 `_index.txt`、新建/修改单位行 |
| `EnemyInfoExcelTool` | 敌人信息 Excel 工具 |
| `EnemySpineDownloadTool` | 敌人 Spine 下载 |
| `SpineDownLoadTool` | 干员 Spine 下载 |
| `TokenDownloadTool` | 召唤物/Token 下载 |
| `StandPicDownLoadTool` | 立绘下载（PRTS Wiki） |
| `HalfDownLoadTool` | 半身像下载 |
| `ResourcesExportTool` | 资源导出 |
| `ABExportTool` | AssetBundle 导出 |
| `MapInfoExChangeTool` | 地图信息转换 |
| `SpineBoneFindTool` | Spine 骨骼查找 |

## 4. 配置类生成注意事项

- `ExportClass` 会把 `Config/*.cs` 中对应 Sheet 的类**整体覆盖**；若在配置类里手写了属性，请先确认 Sheet 里有对应列，否则会丢失。
- 生成类形如：
  ```csharp
  public class UnitData : IConfig {
      public string Id { get; set; }
      public string Name;
      public int Type;
      ...
  }
  ```
  引用其他表的字段会自动变成 `int?` 或 `int[]`（下标引用）。
- 配置类手写扩展在 `Config/Ex/`（如 `SkillDataEx`、`RewardDataEx`），用于补充计算属性/扩展方法。
- 注意：导表不会生成 `SkillJsonData` 的可读配置，`SkillJson` 以 `Data/SkillJson.txt` / 热更分类目录为准。

## 5. 辅助工具

- `Helper/JsonHelper.cs`：Newtonsoft 封装（`ToJson` / `ToJsonWithType` / `FromJson`）。
- `Helper/FileHelper.cs`：递归文件遍历。
- `Helper/ResHelper.cs`：资源加载与预加载。
- `Helper/Log.cs`：统一日志。
- `Helper/UnifiedExpressionEngine.cs`：基于 System.Linq.Dynamic.Core 的表达式引擎（过滤/计算/赋值），带编译缓存与成员缓存。
