# ExcelConfigChecker — Excel 配置数据检查工具

独立于 Unity 编辑器运行的 Excel 配置检查工具，使用 Python 3 + Tkinter 构建 GUI，可打包为单文件 `ExcelConfigChecker.exe`。

## 1. 功能

1. **多文件统一检查**
   - 可一次选择一个或多个 `.xlsx` / `.xlsm`；
   - 多个 Excel 合并为同一套配置，重复 Id、跨表引用在所有所选文件之间统一解析；
   - 未选择的表不会被加载，因此只选部分 Excel 时，指向未选表中 Id 的引用会被判为无效引用。

2. **重复 Id 检查**
   - 按 Sheet 名分组，跨文件检测重复 Id；
   - 报告重复 Id 所在文件、Sheet、行号，以及首次出现位置；
   - `UnitData` 的 `Id=0` 视为导出器占位行，不参与重复检查。

3. **无效引用检查**
   - 当第 3 行字段类型是某个 Sheet 名（如 `BuffData`、`EffectData`、`BuffData[]`）时，检查 Id 是否存在于目标表；
   - 支持标量引用和数组引用（逗号/换行分隔）；
   - 目标表完全没有被选中时，该项引用校验跳过并给出警告，避免把“未选择表”误报成大量无效 Id。

4. **基础数据类型检查**
   - 依据每个 Sheet 第 3 行声明的类型检查第 4 行起的数据；
   - 支持：`int/int32/int64/long`、`float/double`、`bool`、`string`；
   - 支持数组：`int[]`、`float[]`、`bool[]`、`string[]` 等；
   - 支持 Unity 类型：`UnityEngine.Vector2/Vector2Int/Vector3/Vector3Int/Rect` 及其数组；
   - 支持可空写法：`float?`、`int?`；
   - 枚举类型（以 `Enum` 结尾）只统计、不校验值域，因为工具没有加载 C# 枚举定义。

5. **Type 合法性检查**
   - 检查 `UnitData`、`SkillData`、`BuffData`、`ModifyData`、`BulletData` 的 `Type` 列；
   - 运行期映射规则：
     - `UnitData.Type` → `Units.<Type>`
     - `SkillData.Type` → `Skills.<Type>`（特殊值 `Json` 表示 `JsonSkill`）
     - `BuffData.Type` → `Buffs.<Type>`
     - `ModifyData.Type` → `Modifys.<Type>`
     - `BulletData.Type` → `Bullets.<Type>`
   - 合法 Type 由工具同级目录的 `type_whitelist.txt` 维护；
   - 配置文件按表分区，带中文注释和子类分类，支持手动增删；
   - 空 Type、首尾带空白的 Type、不在白名单中的 Type 都会报错；
   - 支持 GUI 辅助功能：选择一个确认合法的 Excel，工具会扫描其中 5 张表的全部 Type，并自动合并进白名单配置。

6. **Dictionary/Data JSON 检查**
   - 对类型包含 `System.Collections.Generic.Dictionary` 的字段进行 JSON 合法性检查；
   - 对类型为 `Data` 的字段也按 JSON 检查；
   - 检查 JSON 语法、顶层是否为对象、重复键（重复键作为警告）；
   - 拒绝 `NaN` / `Infinity` 等非标准 JSON 常量。

7. **检查结果输出**
   - GUI 中按检查项分组显示错误/警告，错误红色、警告橙色；
   - 每次检查自动在 **exe 同级目录**生成 `ExcelConfigCheckReport_时间戳.txt`；
   - 报告使用 UTF-8 BOM 编码，Windows 记事本可直接打开中文。

8. **检查项独立开关**
   - 重复 Id 检查；
   - 无效引用检查；
   - 基础数据类型检查；
   - Type 合法性检查；
   - Dictionary/Data JSON 检查；
   - 支持“全部开启/全部关闭”。

## 2. 使用方式

### GUI

双击 `ExcelConfigChecker.exe`：

1. 点击“添加文件...”选择一个或多个 Excel；
2. 按需勾选/取消检查项；
3. 点击“开始检查”；
4. 在下方查看结果，报告自动保存到 exe 同级目录。

Type 白名单维护：

- 点击“扫描合法 Excel 更新配置...”，选择一个确认可用的 Excel；
- 工具会扫描其中 `UnitData/SkillData/BuffData/ModifyData/BulletData` 的全部非空 Type，并合并进 `type_whitelist.txt`；
- 点击“打开配置文件”可直接编辑；删除某行即禁止该 Type，新增一行即允许新 Type；
- 如果 `type_whitelist.txt` 不存在，首次运行会自动从内置模板生成。

### 命令行（便于 CI 或批量检查）

```bat
ExcelConfigChecker.exe --cli a.xlsx b.xlsx --out report.txt
```

可用开关：

```text
--no-dup             关闭重复 Id 检查
--no-ref             关闭无效引用检查
--no-type            关闭基础数据类型检查
--no-type-whitelist  关闭 Type 合法性检查
--no-json            关闭 Dictionary/Data JSON 检查
--type-config PATH   指定 Type 白名单文件（默认 exe 同级 type_whitelist.txt）
--import-types XLSX  扫描合法 Excel 并合并 Type 到白名单后退出
```

退出码：`0` = 无错误；`1` = 存在错误。

示例：

```bat
# 扫描一个确认合法的 Excel，把其中 Type 合并进白名单
ExcelConfigChecker.exe --import-types battle.xlsx

# 用自定义白名单做检查
ExcelConfigChecker.exe --cli a.xlsx --type-config my_type_whitelist.txt
```

## 3. 数据格式约定

与工程导表规则一致：

| Excel 行 | 含义 |
|---|---|
| 第 1 行 | 中文说明 |
| 第 2 行 | 字段名 |
| 第 3 行 | 字段类型 |
| 第 4 行起 | 数据，首列为 Id |

- `#` 开头的 Sheet 或 Id 行按注释跳过；
- 空 Id 行跳过并计数；
- `~$xxx.xlsx` 等 Excel 临时文件自动跳过；
- 仅支持 `.xlsx` / `.xlsm`，不支持旧版 `.xls`。

## 3.1 Type 白名单文件

`type_whitelist.txt` 与 exe 同级，格式如下：

```text
[UnitData]
# 单位类型
干员
敌人
中立单位
普通单位

[SkillData]
# 技能类型
普通技能
Json

[BuffData]
# Buff 类型
普通Buff
数值变化
...
```

- `[UnitData]`、`[SkillData]`、`[BuffData]`、`[ModifyData]`、`[BulletData]` 五个分区；
- 每行一个 Type，区分大小写；
- `#` 或 `;` 开头为注释；
- 支持 `Type = 备注` 写法（等号左侧为 Type，右侧写说明；左侧为空时取右侧）；
- 可用 GUI 扫描功能自动补充，也可手动删改。

## 4. 已知限制

1. 枚举只统计字段数量，不校验枚举值是否合法；
2. 字符串内包含双引号/换行等会破坏导表 JSON 的情况，不在本工具检查范围内；
3. 只选择部分 Excel 时，跨文件引用可能被报为无效 Id，请选择完整表集；
4. Type 合法性依赖 `type_whitelist.txt`，新增子类后必须更新白名单，否则会被报为非法 Type；
5. `SkillData` 的 `Type=Json` 是特殊合法值，白名单中已内置；
6. 公式单元格若没有缓存值，读取结果可能为空，请先用 Excel 打开并保存一次；
7. 工具不修改任何 Excel 文件，只读取并生成检查报告；扫描 Type 时只修改白名单配置文件。

## 5. 源码与构建

- 源码：`excel_config_checker.py`
- Type 白名单：`type_whitelist.txt`
- 测试：`tests/test_checker.py`
- 构建脚本：`build_exe.bat`（会把 `type_whitelist.txt` 一起打进 exe，首次运行自动释放到 exe 同级目录）

```bat
python -m unittest discover -s tests -v
build_exe.bat
```

构建依赖：Python 3.10+、Tkinter、openpyxl、PyInstaller。
