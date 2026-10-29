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
| `UnityEngine.Vector3[]` / `Vector3Int[]` | `[{"x":1,"y":2,"z":3},...]`，点之间用 `#` 分隔 |
| `UnityEngine.Rect` | `{"x":1,"y":2,"width":3,"height":4}` |
| `UnityEngine.Rect[]` | `[{"x":1,"y":2,"width":3,"height":4},...]`，点之间用 `#` 分隔 |
| `Data` / `System.Collections.Generic.Dictionary<...>` | 严格 JSON 对象；`Data` 会按 `{...}` 包装后校验 |
| `float?` / `int?` 等可空类型 | 校验时归一化为基础类型；导出仍按原值写出 |

### 1.3 导出方式

1. **编辑器菜单导出**：`Tools → 导出配置`（`Assets/Editor/ExcelEditor/ExcelExportEditor.cs`）
   - 输入目录：`./Excel` 下**所有** `.xlsx`（递归），跳过 `$` 临时文件与备份目录；
   - 生成配置类：`Assets/Scripts/Config/<SheetName>.cs`（注意：会覆盖）；
   - 合并所有同名 Sheet 并导出到 `Assets/Bundles/Data/<SheetName>.txt`（每行一个 JSON）；`UnitData` 首行写入 `Id=0` 占位行；
   - 自动 `AssetDatabase.Refresh()`。
2. **游戏内导出**：主界面勾选玩家 Excel → 点击导出，走 `ExcelHelper.Export(ExcelList)`（`Assets/Scripts/Helper/ExcelHelper.cs`），**返回 bool**。
   - 用于玩家自定义表（DIY/一方通行/尊尼获加等），运行时把选中的 Excel 导出到热更路径；
   - 先并行写入 `Data_ExportTemp_<guid>/` 临时目录，全部成功后再复制到正式 `Data/<SheetName>/<Excel文件名>.txt`，并生成 `Data/<SheetName>/_index.txt`；
   - 任一步校验/导出失败返回 `false`，`UI_Main` 不会移除排除列表、不会重载 `Database`，并提示“导表失败，已保留原数据”；
   - 只有成功后才会 `Database.Clear()` → `Database.Init()` 重新加载，并 `GameData.RefreshCardData()`。

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
  引用其他表的字段会生成旧版 `int?` / `int[]`，以及新的 `[ConfigRef] string *Id / *Ids`。
- 配置类手写扩展在 `Config/Ex/`（如 `SkillDataEx`、`RewardDataEx`），用于补充计算属性/扩展方法。
- 注意：导表不会生成 `SkillJsonData` 的可读配置，`SkillJson` 以 `Data/SkillJson.txt` / 热更分类目录为准。

### 4.1 引用字段与字符串 Id（2026-10-28）

- 引用其他表的字段会同时生成两套字段：
  - 旧版兼容：`int?` / `int[]`（数组下标）
  - 新版稳定引用：`[ConfigRef("目标表")] string XxxId` / `string[] XxxIds`
- 运行时 `Database` 会把 `*Id/*Ids` 解析回旧 int 字段，因此旧代码仍然可读。
- 导出前会检查重复 Id、跨表引用不存在、基础数值非法；空 Id 行会跳过（视为临时数据），有问题会弹窗并阻止导出。
- 导出日志与校验报告统一写到 `PathHelper.ExportLogPath`（编辑器为工程 `Logs/`；非编辑器桌面为 `StreamingAssets/Logs/`；移动端为 `persistentDataPath/<productName>/Logs/`）。
- 详细设计与迁移动见 [28-Excel配置字符串Id迁移与导表校验](28-Excel配置字符串Id迁移与导表校验.md)。

### 4.2 Type 合法性检查（2026-10-30）

- 新增 `Assets/Scripts/Core/ConfigTypeRegistry.cs`，通过反射枚举以下命名空间下的可实例化子类，不再维护整份文字白名单：
  - `UnitData` → `Units.<Type>` : `Unit`
  - `SkillData` → `Skills.<Type>` : `Skill`，特殊值 `Json` → `JsonSkill`
  - `BuffData` → `Buffs.<Type>` : `Buff`
  - `ModifyData` → `Modifys.<Type>` : `Modify`
  - `BulletData` → `Bullets.<Type>` : `Bullet`
- 过滤规则：`public class`、非抽象、非泛型定义、非嵌套、基类匹配、存在 public 无参构造函数。
- `ExcelHelper.ValidateExcelFiles` 逐行检查五张表的 `Type` 列；空 Type、首尾空白、类名/命名空间不存在都会阻断导出。
- 反射结果按表缓存；`ReflectionTypeLoadException` 会降级到可加载类型并输出错误；结果为空时直接报错，避免“空集合导致全部通过/全部失败”。
- 特殊别名与设计性排除集中在 `ConfigTypeRegistry.ManualAliases / ManualExclusions`，只需维护极少量例外，不再维护完整白名单。
- 若未来切换 IL2CPP，`Assets/link.xml` 已保留 `Units/Skills/Buffs/Modifys/Bullets` 命名空间，避免反射类型被裁剪。

### 4.3 导表校验 P0 完成与剩余待办（2026-10-30）

#### 本轮已完成 P0

- **Dictionary/Data JSON**：新增 `Assets/Scripts/Core/StrictJsonValidator.cs`，对 `System.Collections.Generic.Dictionary` 和 `Data` 字段做严格 JSON 校验；不使用宽松的 `JToken.Parse`，会拒绝尾随逗号、单引号、`NaN/Infinity`、非法数字格式和多余内容。
- **数组类型**：校验 `int[] / float[] / bool[]` 等数组的元素类型和空元素。
- **Vector / Rect**：校验 `UnityEngine.Vector2/Vector2Int/Vector3/Vector3Int/Rect` 的分量数量和分量数值类型，并支持对应数组。
- **可空类型**：校验前归一化 `float? / int?` 等写法（`GetBaseType` / `NormalizeTypeToken`）。
- **导出失败传播**：`ExcelHelper.Export` 改为返回 `bool`；`UI_Main` 只在成功时移除排除列表、重载 Database、提示“数据更新完成”。
- **部分导出失败处理**：`ExportData` 先写入 `Data_ExportTemp_<guid>` 临时目录，全部 Excel 成功后才覆盖正式 `Data`；任一文件失败则不写 `_index.txt` 并返回失败。

#### 剩余 P1 待办

1. **引用边界**：目标表完全未选中时目前仍静默跳过，建议给出警告；引用类型字符串 Trim/归一化仍需与 `Convert`/`AppendReferenceIdFields` 统一（引用数组空项已在 `ValidateExcelFiles` 中检查）。
2. **枚举值域**：`XxxEnum`/`XxxEnum[]` 仍未校验值域；`Convert` 对枚举解析失败会原样输出，可能写出非法 JSON 或错误数据。
3. **字符串 JSON 转义**：`string` / `string[]` 内的双引号、换行尚未检查，导出时可能破坏 JSON。
4. **数值格式与范围**：当前 `int/float` 校验接受 `+1`、`01`、`1.`、`.5` 等 .NET 可解析但 JSON 非法的写法，建议统一使用严格 JSON number 语法；`uint/ulong` 目前也按 `long` 解析，极端大值需要单独处理。
5. **未知字段类型**：未知类型、拼写错误的类型目前静默跳过，建议汇总为警告/错误。
6. **导出后校验**：尚未重新解析写出的 JSON，也未校验 `_index.txt` 与数据文件一一对应；删除/重命名 Excel 后的旧分类文件不会自动清理；正式 Data 覆盖仍不是全目录原子替换，复制/写索引中途异常仍可能留下部分新文件。
7. **编辑器导表入口**：`ExcelExportEditor.ExportData` 仍直接写正式目录，未使用临时目录，也没有返回值传播；建议与 `ExcelHelper.Export` 统一。
8. **Config 类生成**：字段名是否为合法 C# 标识符、同 Sheet 重复字段名、未知类型是否会生成无法编译的 C#，尚未校验；`ExportClass` 整体覆盖 `Config/*.cs` 的手写字段/默认值保护仍有限。

#### 剩余 P2 待办

- 结构检查：Id 是否位于第一列、字段名/类型列为空、重复列、空 Id 但包含数据等结构问题仍只做跳过/计数。
- 语义检查：`Data` 字典内部键值类型/必填键、`SkillCondition/OrderExpression` 表达式、数值业务范围（负生命、零速度、非法 Team 等）未校验。
- 运行期防御：`Battle.CreateSceneUnit/CreatePlayerUnit/CreateEnemy/CreateBullet`、`ModifyManager.Get(int,Bullet)` 对反射创建结果仍缺少 null 防御。
- 反射边界：编辑器与真机条件编译类型差异、未来拆分 asmdef 时需要更新 `ConfigTypeRegistry.CandidateAssemblies`；`SkillData` 空 Type + `SkillJsonData` 的兼容特例仍需明确策略。
- 报告/日志：成功导出后旧的 `ExcelValidationReport.txt` 不会自动清理，容易误读。

## 5. 辅助工具

- `Helper/JsonHelper.cs`：Newtonsoft 封装（`ToJson` / `ToJsonWithType` / `FromJson`）。
- `Helper/FileHelper.cs`：递归文件遍历。
- `Helper/ResHelper.cs`：资源加载与预加载。
- `Helper/Log.cs`：统一日志。
- `Helper/UnifiedExpressionEngine.cs`：基于 System.Linq.Dynamic.Core 的表达式引擎（过滤/计算/赋值），带编译缓存与成员缓存。
