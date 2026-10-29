# 28 · Excel 配置字符串 Id 迁移与导表校验

> 编写时间：2026-10-28
> 适用工程：`zhou-master`（Unity 2021.3.39f1）
> 关联文档：[07-工具链与Excel导表](07-工具链与Excel导表.md)、[24-ArkFx系统现状与能力边界](24-ArkFx系统现状与能力边界.md)

---

## 0. 背景

上一轮排查“重构体中毒 / 重构体加攻”特效错乱时，根因是：

```text
BuffData 中出现重复 Id
  → _index.txt 保留重复索引槽位
  → Database.BuildConfigArray 用 Dictionary 按 Id 合并
  → 重复槽位全部指向同一份“最后/第一条”定义
  → LastingEffect int 索引指向错误或为空的 EffectData
```

同类问题还包括：

- `LastingEffect`、`Buffs`、`Skills` 等字段仍是 int 数组下标；
- EffectData / BuffData 顺序变化会让 int 索引静默指向另一行；
- Excel 导出缺少重复 Id、无效引用等硬性校验（空 Id 行作为临时数据跳过）；
- 多 Excel 文件导出时，文件枚举顺序会影响最终生效数据。

本轮目标：

1. 新增稳定的字符串 Id 引用层，旧格式数据仍可加载；
2. 运行时把字符串 Id 解析回旧 int 字段，避免大规模改所有调用方；
3. Excel 导出前做基础校验，重复/非法数据弹窗并阻止导出；
4. 提供现有数据的字符串 Id 迁移工具。

---

## 1. 核心设计

### 1.1 稳定 Id 与新引用字段

`IConfig.Id` 本来就是字符串：

```csharp
public interface IConfig
{
    string Id { get; }
}
```

新导出数据会在原有 int 字段之外，额外写入：

```text
BuffData.LastingEffect     （旧 int 索引）
BuffData.LastingEffectId   （新稳定字符串 Id）
```

数组引用同理：

```text
SkillData.Buffs     （旧 int[]）
SkillData.BuffsIds  （新 string[]）
```

### 1.2 `ConfigRefAttribute`

新增：

```csharp
[ConfigRef("EffectData")]
public string LastingEffectId;

[ConfigRef("BuffData")]
public string[] BuffsIds;
```

`ConfigRefAttribute.Table` 说明目标表名。

当前已标注的字段包括：

- `CardData.units` → `UnitData`
- `UnitData.Skills / MainSkill / IgnoreBuff`
- `SkillData` 的 `Buffs / BuffRemoves / Modifys / ModifyDatas / Skills / ExSkills / Bullet / 各种 Effect`
- `BuffData.LastingEffect / Upgrade / RelyBuff`
- `BulletData.Modifys`
- `ModifyData.Buff`
- `ContractData.Skills`
- 等等

### 1.3 运行时兼容绑定

`Database.BuildLookupAndBind()` 在加载完成后：

1. 建立 `Type -> Id -> config` / `Type -> Id -> index` 索引；
2. 扫描带 `[ConfigRef]` 的 `string/string[]` 字段；
3. 把字符串 Id 解析成目标表的数组索引；
4. 回填旧字段：

```text
BuffData.LastingEffectId = "重构体加攻特效"
  → 解析 EffectData 索引 1067
  → 回填 BuffData.LastingEffect = 1067
```

因此：

- **新数据**：以字符串 Id 为准，旧 int 字段被自动纠正；
- **老数据**：没有 `*Id` 字段，保持原来的 int 索引行为，完全兼容；
- **旧代码**：不用一次性改完所有 `Database.Get<T>(int)` 调用。

### 1.4 重复 Id 的运行策略

`Database.BuildConfigArray` 现在：

- 发现重复 Id → `AddInitError`，运行时会通过 `TipManager` 提示；
- 采用 **第一条定义获胜** 的确定性策略；
- `ResolveDataFiles` 对文件路径排序，避免 Windows / Android / 不同文件系统的枚举顺序导致结果不一致。

> 注意：这是“旧数据兼容的兜底”。新数据不允许再产生重复 Id，Excel 导出层会阻止。

---

## 2. Excel 导出校验

### 2.1 校验入口

`ExcelHelper.ValidateExcelFiles(List<string>)` 统一实现基础检查：

| 检查 | 行为 |
|---|---|
| 空 Id | 跳过并计入“忽略临时行”数量，不阻断导出 |
| 重复 Id | 同一 Sheet 多次出现同 Id → 报错，并显示首次出现位置 |
| 引用不存在 | `EffectData / BuffData / SkillData / UnitData ...` 类型列的 Id 在目标表中找不到 → 报错 |
| 基础数值非法 | `int/float/bool` 无法解析 → 报错 |

> 2026-10-30 更新：`ValidateExcelFiles` 已进一步加入 `ConfigTypeRegistry` 反射 Type 校验、`StrictJsonValidator` 严格 JSON/Data 校验、数组/Vector/Rect/可空类型校验，并接入导出失败返回与临时目录写入。剩余待补项见 [07-工具链与Excel导表](07-工具链与Excel导表.md) 的“4.3 导表校验 P0 完成与剩余待办”。

错误信息统一带位置，例如：

```text
Excel "Mon3ter.xlsx" → Sheet "BuffData" → Id "重构体中毒" → 第 2 行：重复 ID（首次出现于 Mon3ter.xlsx:2）
Excel "battle.xlsx" → Sheet "SkillData" → Id "某技能" → 字段 "Buffs"：无效 ID "不存在的Buff"（目标表 BuffData 中不存在）
```

空 Id 行不会报错，只会在 Console 输出：

```text
[ExcelHelper] 忽略 N 行空 Id 临时数据（导出时会跳过）
```

`UnitData` 的 `Id=0` 是导出器内置占位行（会生成 `0.txt`），Excel 中的该行也会被跳过，不计为重复 Id：

```text
[ExcelHelper] 忽略 N 行 UnitData Id=0 占位数据（导出器会生成 0.txt）
```

完整错误会同时写入 `PathHelper.ExportLogPath` 下的：

```text
ExcelValidationReport.txt
```

导出过程本身还会写：

```text
ExcelExport.log
```

日志目录规则：

| 环境 | 目录 |
|---|---|
| 编辑器 | `<工程根>/Logs` |
| 非编辑器桌面 | `PathHelper.AppHotfixResPath/Logs`（桌面端即 `StreamingAssets/Logs`） |
| 移动端 | `persistentDataPath/<productName>/Logs` |

### 2.2 Editor 入口

`Tools/导出配置`（`ExcelExportEditor.ExportAll`）：

```text
扫描 Excel
  → ExcelHelper.ValidateExcelFiles
  → 有错误：EditorUtility.DisplayDialog("配置校验失败", ...)
  → 阻止导出
  → 无错误：生成 Config 类 + Bundles/Data
```

### 2.3 运行时/游戏内重导入口

`ExcelHelper.Export(List<string>)` 同样先调用校验：

```text
有错误
  → Debug.LogError 完整错误列表（Console）
  → TipManager 弹出“配置校验失败”
  → 不写 StreamingAssets/Data
```

### 2.4 双字段导出

- `ExcelExportEditor.ExportClass` 生成：
  - 旧 int 字段；
  - 对应 `[ConfigRef]` 的 string Id 字段。
- `ExcelExportEditor.ExportData` / `ExcelHelper.Export` 同时写：
  - 旧 JSON 索引字段；
  - 新 `*Id / *Ids` 字段。

这样：

- 老版本代码读 int 仍正常；
- 新代码可以逐步改为 `Database.Get<T>(string id)`；
- 数字索引顺序变化不会破坏新数据。

---

## 3. 新增代码/工具

| 文件 | 作用 |
|---|---|
| `Assets/Scripts/Config/ConfigRefAttribute.cs` | 标记 string Id 引用字段及目标表 |
| `Assets/Scripts/Database.cs` | `byId/indexById` 索引、`Get<T>(string)`、`GetId<T>(int)`、字符串引用回填、重复 Id 检测 |
| `Assets/Scripts/VFX/EffectManager.cs` | 新增 `GetEffect(string effectId)` 稳定 Id 入口 |
| `Assets/Scripts/Core/Buff/Buff.cs` | 优先使用 `BuffData.LastingEffectId`，并对 LastingEffect 为空做保护 |
| `Assets/Editor/ExcelEditor/ExcelExportEditor.cs` | 导出前校验、弹窗阻止、生成 Id 字段、写出 Id 字段 |
| `Assets/Scripts/Helper/ExcelHelper.cs` | 同名校验、TipManager 提示、Id 字段写出 |
| `Tools/migrate_config_refs.py` | 已有旧 JSON 数据迁移工具（默认 dry-run） |

---

## 4. 当前数据迁移

### 4.1 dry-run 检查

```bash
python Tools/migrate_config_refs.py
```

输出包括：

- 扫描行数；
- 生成 Id 字段行数；
- 无法解析的旧索引 / 缺少 `_index` 的引用。

当前工作区执行结果（2026-10-28）：

```text
DRY-RUN: rows=4248, rows_with_id_fields=3510, errors=55
```

其中 55 个错误主要是：

- `_index.txt` 与分类数据文件不同步；
- 部分 Skill/Buff 引用的索引超出当前目标 `_index` 长度；
- 典型受影响数据：`莫菲丝.txt` 的 Buff 引用。

这些错误必须先修表/修索引，不能靠迁移工具“猜”。

### 4.2 写入当前分类数据

```bash
# 只写 StreamingAssets/Data（编辑器内运行时的主读取位置）
python Tools/migrate_config_refs.py --apply
```

当前工作区已对 `Assets/StreamingAssets/Data` 执行过该命令（2026-10-28），因此一些行已经变成：

```json
{
  "Id": "重构体加攻",
  "LastingEffect": 1067,
  "LastingEffectId": "重构体加攻特效"
}
```

`Assets/Bundles/Data` 没有做批量迁移；它会由修复后的 `Tools/导出配置` 重新生成。

### 4.3 同时写统一 Bundles/Data

```bash
python Tools/migrate_config_refs.py --apply --include-bundles
```

> 不建议在重复 Id / 越界引用清理前执行全量迁移。

---

## 4.4 静态验证

```bash
python Tools/verify_config_id_support.py
```

期望：

```text
判定: [OK] 字符串 Id 兼容层/导出校验静态检查全部通过
```

该脚本检查：

- `ConfigRefAttribute` 存在；
- `Database` 有字符串 Id 索引/回填；
- `ExcelHelper` / `ExcelExportEditor` 有重复与非法数据校验；
- 导出层会写 `*Id/*Ids`；
- `Buff` / `EffectManager` 有字符串 Id 入口；
- 当前已迁移的「重构体」Buff 数据包含 `LastingEffectId`。

---

## 5. 人工验收

### 5.1 导出校验弹窗

1. 暂时在 Excel 中复制一条已有 Id，制造重复定义。
2. 菜单 `Tools/导出配置`。
3. 期望：
   - 弹出「配置校验失败」对话框；
   - 显示重复 Id 的文件 / Sheet / 行号；
   - 不生成新的 Config 类和数据文件。

### 5.2 修复后正常导出

1. 删除重复行。
2. 再执行 `Tools/导出配置`。
3. 检查生成的 `Assets/Scripts/Config/BuffData.cs` 是否包含：

```csharp
[ConfigRef("EffectData")] public string LastingEffectId;
```

4. 检查 `Assets/Bundles/Data/BuffData.txt` 同一行是否同时有：

```json
"LastingEffect":1067,"LastingEffectId":"重构体加攻特效"
```

### 5.3 运行时兼容

1. 重新进 Play。
2. 触发「重构体」相关 Buff。
3. 检查日志：

```text
[Buff] 重构体中毒 effectIndex=1067 effect=monstr_skill_01_buff(Clone)
[Buff] 重构体加攻 effectIndex=1067 effect=monstr_skill_01_buff(Clone)
```

4. 确认不再出现 `thorns_buff_01_start` / `effect=NULL`。

### 5.4 旧格式兼容

1. 保留一份没有 `*Id` 字段的旧 JSON。
2. 直接加载。
3. 期望：
   - `Database` 不报错；
   - 旧 int 引用保持原有行为；
   - 新代码 `Database.Get<T>(string id)` 仍可用。

---

## 5.5 当前数据状态提醒

当前 `Excel/` 全量按同一规则扫描时，仍能发现重复 Id 和部分引用缺失（不同 Sheet 的测试数据/旧数据混杂）；空 Id 行会被跳过，不阻断导出。
因此本次升级后第一次执行 `Tools/导出配置` 会**按设计弹窗并阻止导出**，这是预期行为：

1. 先清理重复 Id 行；
2. 修复 `_index.txt` 与分类数据不一致；
3. 再重新导出。

不要通过绕过校验来生成新数据。

---

## 6. 已知限制

1. **当前 `Config` 类和旧数据同时保留 int 字段**：这是兼容层，不是最终形态。后续可把高频引用逐步改成只用 `*Id`。
2. `DungeonLevelData / EventData / RewardData / RelicData / MapTileData / SystemData` 目前仍没有注册到 `Database.Init`；它们的引用字段不在本轮迁移范围。
3. 旧数据中的重复 Id / `_index` 与分类文件不同步问题不会自动修复，校验工具只负责报出来。
4. `migrate_config_refs.py` 不会自动决定重复 Id 哪份是正确数据；必须人工确认后清理 Excel。
5. 当前 `ValidateExcelFiles` 是基础校验，不替代 JSON Schema、字段长度、业务语义校验。

---

## 7. 推荐后续

1. 先清理 `Excel` 中的重复 Id；
2. 修复 `_index` 与分类数据不同步问题；
3. 全量执行 `Tools/导出配置`；
4. 全量执行 `ExcelHelper.Export` / 对应导出入口；
5. 逐步把 `Buff / Skill / Unit / Bullet / Effect` 的核心访问点改成 `Database.Get<T>(string id)`；
6. 为 `Dungeon` 等未注册表补 `Database.Init` 注册与对应校验。
