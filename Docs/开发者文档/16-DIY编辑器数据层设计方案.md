# 16 DIY 编辑器数据层设计方案

> 版本：v1.0（2026-10-25）
> 适用工程：`zhou-master`
> 相关文档：
> - `02-启动流程与数据流.md`
> - `07-工具链与Excel导表.md`
> - `14-JsonSkill开发者文档.md`
> - `15-特效系统优化方案.md`
> - `16-DIY编辑器数据层设计方案.md`（本文）
>
> 本文覆盖：
> - DIY 编辑器数据加载
> - 配置组（DiyPack）数据结构
> - 保存与运行时编译
> - 旧配置数据兼容与反向生成
> - 字段 Schema / 默认值 / 引用转换
> - 分阶段实施计划与完整任务清单

---

## 1. 背景

DIY 编辑器原本以 Excel 为数据源：

```text
Excel/<作者>/<文件名>.xlsx
    ↓ 导出
Assets/Bundles/Data/<Table>.txt
    ↓ 玩家导表
热更目录/Data/<Table>/<Excel文件名>.txt
    ↓ Database
IConfig[]
```

当前已经明确的新目标是：

1. **彻底解绑 Excel 与运行时数据。**
2. 新建单位、修改单位全部通过 DIY 编辑器完成。
3. 数据文件只保留 JSON 文本。
4. DIY 编辑器需要同时兼容：
   - 旧版按 Excel 文件导出的配置数据；
   - 新版以“配置组”为最小单位的配置数据。
5. UI 布局已经初步定型，不新增独立配置组列表区域，继续复用：
   - `folders` 下拉框
   - `jsonFiles` 下拉框

---

## 2. 当前状态

### 2.1 当前运行时数据目录

```text
Assets/StreamingAssets/Data/
├── UnitData/
│   ├── battle.txt
│   ├── 萃儿.txt
│   └── _index.txt
├── SkillData/
│   ├── battle.txt
│   ├── 萃儿.txt
│   └── _index.txt
├── BuffData/
├── ModifyData/
├── BulletData/
├── EffectData/
├── CardData/
├── ContractData/
├── SpineData/
└── ...
```

单文件内容为 JSONL：

```json
{"Id":"源石虫","Type":"敌人","Model":"enemy_1007_slime_2"}
{"Id":"狗","Type":"敌人","Model":"enemy_1000_gopro"}
```

### 2.2 当前 `_index.txt`

每个表目录有 `_index.txt`：

```text
0
恶堕凛视
非线性移动
时蒙
```

`Database` 按 `_index.txt` 顺序构造 `IConfig[]`，因此：

> **配置 Id 在全局数组中的下标是当前引用字段的唯一语义。**

例如：

```json
{
  "Id": "char_002_amiya",
  "Skills": [12, 15, 18],
  "MainSkill": [18]
}
```

这里的 `12` 不是文件行号，而是 `SkillData` 全局下标。

### 2.3 当前 DIY 读取状态

已完成的临时迁移：

```text
Assets/Scripts/UI/DIY/UI_Main.cs
Assets/Scripts/UI/DIY/UI_Main.Json.cs
```

当前行为：

- `folders` 读取 `StreamingAssets/Data` 下的配置文件夹；
- `jsonFiles` 读取当前文件夹下 `.txt` / `.json` 文件；
- 排除 `_index.txt`、`_index.json`、`.meta`；
- 支持 JSONL 与 JSON 数组；
- 使用 `JsonHelper.FromJson<UnitData>` 解析；
- 单位选择不再依赖 `Database`。

当前保存行为：

- `save2Excel` 已停止写入 Excel；
- 仅提示“保存功能尚未迁移到 JSON 数据源”。

### 2.4 当前 Schema 工具状态

已存在：

```text
Assets/Editor/DIY/DiySchemaGeneratorWindow.cs
Assets/Editor/DIY/DiyValueConverter.cs
```

生成：

```text
Assets/Bundles/Data/DIY/DiySchema.json
```

Schema 版本 2 包含：

- 每个配置类一个分页；
- UnitData 三个 variants：
  - `opreator` 干员
  - `enemy` 敌人
  - `normal` 中立单位
- 每字段：
  - 字段名
  - 类型
  - 中文备注
  - 是否默认属性
  - 分组
  - 默认值原始输入
  - 默认值 JSON
- 配置类级禁用开关。

---

## 3. 当前问题

### 3.1 Excel 文件名承担的隐式配置组作用

旧数据中：

```text
Data/UnitData/battle.txt
Data/SkillData/battle.txt
Data/BuffData/battle.txt
```

这里 `battle` 实际上是这些配置的共同来源，也就是一个隐式“配置组”。

舍弃 Excel 后，需要把这个隐式关系显式化。

### 3.2 只按类型分文件夹无法表达归属

`UnitData/char_002_amiya.txt` 和 `SkillData/char_002_amiya.txt` 虽然同名，但：

- 关系依赖文件名约定；
- 编辑器需要跨多个文件夹扫描；
- 保存时需要同时改写多个文件；
- 删除时容易遗漏。

### 3.3 纯 UnitData 锚点无法覆盖共享配置

以下内容不适合强行归属到单个 UnitData：

- 全局技能 `SkillData.Type == "全局技能"`
- 多个单位共用的 Buff / Bullet / Effect / Modify
- 多个单位共用的 SpineData
- ContractData
- 召唤物、敌人组、地图波次可能引用多个 UnitData

### 3.4 引用字段是全局下标

当前 `Database` 与大量战斗逻辑依赖数组下标：

```text
UnitData.Skills     -> SkillData 下标
UnitData.MainSkill  -> SkillData 下标
CardData.units      -> UnitData 下标
SkillData.Buffs     -> BuffData 下标
SkillData.Bullet    -> BulletData 下标
SkillData.Modifys   -> ModifyData 下标
```

因此：

- 不能随意删除、重排配置；
- 新建记录只能追加；
- 旧数据迁移必须做“下标 → Id”的解析。

### 3.5 配置类类型信息无法区分普通 int 与引用

例如：

```csharp
public int[] Skills;       // 引用 SkillData
public int[] EnableBuff;   // 引用 BuffData
public int RareLimit;      // 普通 int
```

从 C# 类型只能看到 `int` / `int[]`，无法自动知道哪些是配置引用。

因此必须补充：

```text
DiyReferenceMap
```

或扩展 `DiySchemaField`：

```json
{
  "name": "Skills",
  "type": "int[]",
  "referenceType": "SkillData",
  "isArray": true
}
```

### 3.6 当前旧的配置读取中残留问题

- `attributeNames` 手写字段列表；
- `save2Excel` 已停用但保存链路未替换；
- `freshAttribute` 暂时清空，等待接入 `DiySchema.json`；
- 属性值和 UI 之间还没有统一的数据缓冲模型。

---

## 4. 核心概念

### 4.1 DiyPack（配置组）

> DiyPack 是 DIY 编辑器加载、保存、校验、复制、删除的最小单位。

DiyPack 可以包含：

- 一个或多个 UnitData；
- 该 Unit 使用的 CardData；
- 技能、Buff、Modify、Bullet、Effect；
- 可选 SpineData；
- 对其他 Pack 的依赖声明。

单单位场景下：

```text
PackId = UnitData.Id
```

但模型本身不限制一个 Pack 只能有一个 UnitData。

### 4.2 PackId

- PackId 必须全局唯一；
- 建议使用：
  - 单单位 Pack：`char_002_amiya`
  - 旧数据 Pack：原 Excel 文件名，例如 `battle`、`萃儿`
  - 分类目录：`units`、`enemies`、`shared`、`contract`
- PackId 不直接等于任何数组下标。

### 4.3 EntryUnit（入口单位）

`EntryUnit` 是打开 Pack 时默认展示的单位。

- 单单位 Pack：一个 EntryUnit；
- 多单位 Pack：多个 EntryUnit；
- 敌人组 / 召唤组可以没有唯一 EntryUnit，但需要声明主展示单位。

### 4.4 SharedPack（共享 Pack）

用于存放：

- 全局技能；
- 公共 Buff / Bullet / Effect / Modify；
- 公共 SpineData；
- 被多个 Pack 引用的配置。

其他 Pack 通过：

```json
"dependencies": ["shared"]
```

引用共享 Pack。

### 4.5 LegacyPack（旧数据虚拟 Pack）

旧数据不立即物理拆分。

DIY 编辑器扫描：

```text
Data/<Table>/*.txt
```

按文件 stem 分组：

```text
battle.txt -> PackId = battle
萃儿.txt   -> PackId = 萃儿
```

在内存中生成虚拟 Legacy Pack，使旧数据可以立即被 DIY 编辑器读取。

### 4.6 DiyRecordDraft（编辑草稿）

每个被编辑的记录维护：

```csharp
public class DiyRecordDraft
{
    public string TableName;
    public string PackId;
    public string SourceFile;
    public string Id;
    public bool IsNew;

    public JObject Original;
    public JObject Data;

    public HashSet<string> DirtyFields = new HashSet<string>();

    public bool IsDirty => IsNew || DirtyFields.Count > 0;
}
```

原则：

- 新建：从 Schema 默认值初始化 `Data`；
- 修改：从已有 JSON 记录深拷贝到 `Data`；
- UI 直接修改 `Data`；
- 保存时序列化完整 `Data`，不是只保存修改字段。

---

## 5. 新数据结构

### 5.1 Pack 文件

推荐第一版使用单文件 Pack：

```text
Assets/StreamingAssets/Data/Packs/
├── index.json
├── units/
│   ├── char_002_amiya.json
│   └── char_003_kalts.json
├── enemies/
├── shared/
│   └── global.json
├── contract/
│   └── contract_01.json
└── legacy/
    ├── battle.json
    └── 萃儿.json
```

### 5.2 Pack JSON 结构

```json
{
  "packId": "char_002_amiya",
  "displayName": "阿米娅",
  "version": 1,
  "entryUnits": ["char_002_amiya"],
  "dependencies": ["shared"],
  "records": {
    "CardData": [
      {
        "Id": "char_002_amiya",
        "units": ["char_002_amiya"]
      }
    ],
    "UnitData": [
      {
        "Id": "char_002_amiya",
        "Name": "阿米娅",
        "Model": "char_002_amiya",
        "Skills": ["skill_amiya_1", "skill_amiya_2"],
        "MainSkill": ["skill_amiya_2"]
      }
    ],
    "SkillData": [
      {
        "Id": "skill_amiya_1",
        "Type": "普通技能",
        "Buffs": ["buff_amiya_1"]
      },
      {
        "Id": "skill_amiya_2",
        "Type": "普通技能"
      }
    ],
    "BuffData": [
      {
        "Id": "buff_amiya_1",
        "Type": "属性变化"
      }
    ],
    "ModifyData": [],
    "BulletData": [],
    "EffectData": [],
    "SpineData": []
  }
}
```

### 5.3 Pack 内部引用

Pack 内部推荐使用**稳定字符串 Id**：

```json
"Skills": ["skill_amiya_1", "skill_amiya_2"]
```

而不是：

```json
"Skills": [12, 15]
```

原因：

- 可读；
- 不依赖 `_index.txt`；
- 可以安全地复制、合并、校验；
- Pack 编译器再负责转成运行时下标。

### 5.4 Pack 索引

`Packs/index.json`：

```json
{
  "version": 1,
  "packs": [
    {
      "packId": "char_002_amiya",
      "category": "units",
      "displayName": "阿米娅",
      "file": "units/char_002_amiya.json",
      "entryUnits": ["char_002_amiya"],
      "dependencies": ["shared"]
    }
  ]
}
```

索引用于：

- 快速列出 Pack；
- Pack 依赖检查；
- 重复 PackId 检查；
- 迁移报告；
- 编辑器启动时避免扫描全部文件。

---

## 6. UI 与目录映射

不新增配置组列表区域，继续使用：

```text
folders  -> 配置组分类 / 旧数据表目录
jsonFiles -> 配置组文件
```

### 6.1 新模式

```text
Data/Packs/units/char_002_amiya.json
```

- `folders` 显示 `Packs` 或分类；
- `jsonFiles` 显示当前分类下 Pack 文件；
- 选择文件后直接加载 `DiyPack`。

### 6.2 旧模式

```text
Data/UnitData/battle.txt
Data/SkillData/battle.txt
```

- `folders` 显示旧表目录；
- `jsonFiles` 显示文件；
- 选择 `UnitData/battle.txt` 后：
  - 提取 `PackId = battle`；
  - 扫描所有表目录下同名文件；
  - 构建虚拟 Legacy Pack。

### 6.3 Provider 选择

编辑器中增加：

```csharp
public interface IDiyPackProvider
{
    List<string> GetFolders();
    List<string> GetPacks(string folder);
    DiyPack LoadPack(string folder, string packFile);
}
```

实现：

```text
DiyPackProviderFactory
├── LegacyPackProvider
└── DiyPackFileProvider
```

根据路径判断：

- 位于 `Data/Packs/` 下 → `DiyPackFileProvider`
- 位于 `Data/<Table>/` 下 → `LegacyPackProvider`

---

## 7. 数据加载设计

### 7.1 启动流程

```text
UIManager / DIY UI 打开
  ↓
DiySchemaRegistry.Load(DiySchema.json)
  ↓
DiyPackProviderFactory.Create()
  ↓
刷新 folders
  ↓
用户选择 folder
  ↓
刷新 jsonFiles
  ↓
用户选择 pack 文件
  ↓
LoadPack
  ↓
构建 DiyPackView / DiyRecordDraft
  ↓
按 funPage 自动填充属性
```

### 7.2 Schema 加载

`DiySchemaRegistry` 负责：

- 读取 `DiySchema.json`；
- 建立：

```text
className -> variant -> fieldName -> DiyFieldMeta
```

- 提供：
  - `GetField(className, variantKey, fieldName)`
  - `GetAutoFields(className, variantKey)`
  - `GetManualFields(...)`
  - `GetDefaultToken(field)`
  - `GetReferenceType(field)`

### 7.3 Pack 加载

`DiyPackLoader` 负责：

1. 读取 Pack JSON；
2. 校验 `packId`；
3. 校验 `records`；
4. 校验记录 Id 是否重复；
5. 构建：

```csharp
DiyPack
DiyPackView
Dictionary<string, DiyRecordDraft>
```

6. 解析 `entryUnits` 作为默认选中的单位。

### 7.4 旧数据加载

见第 10 章。

---

## 8. 值与类型转换

### 8.1 不使用纯字符串字典作为最终数据

不推荐：

```json
{
  "Hp": "1000",
  "NotUseTile": "1",
  "Skills": "[1,2]"
}
```

推荐：

```json
{
  "Hp": 1000,
  "NotUseTile": false,
  "Skills": [1, 2]
}
```

编辑缓冲使用 `JObject`：

```csharp
draft.Data["Hp"] = 1000;
draft.Data["Name"] = "测试单位";
draft.Data["Skills"] = new JArray(1, 2, 3);
```

### 8.2 DiyValueCodec

新增运行时转换器：

```csharp
public static class DiyValueCodec
{
    public static bool TryParse(string fieldType, string input, out JToken token);
    public static string Format(string fieldType, JToken token);
    public static JToken GetDefaultToken(string fieldType);
}
```

职责：

- UI 字符串 → `JToken`
- `JToken` → UI 字符串
- 默认值初始化
- 清空值处理

### 8.3 类型支持

| 类型 | 输入示例 | 输出 |
|---|---|---|
| int | `1000` | `1000` |
| float | `1.5` | `1.5` |
| bool | `1` / `0` | `true` / `false` |
| string | `测试` | `"测试"` |
| enum | `近战` | 枚举 int |
| int[] | `1,2,3` | `[1,2,3]` |
| string[] | `a,b` | `["a","b"]` |
| Vector2Int | `1,2` | `{"x":1,"y":2}` |
| Vector2Int[] | `0,0#1,0` | `[{"x":0,"y":0},{"x":1,"y":0}]` |
| 引用 | `skill_a` | Pack 内 Id；编译时转下标 |
| 引用[] | `skill_a,skill_b` | Pack 内 Id 数组 |

### 8.4 默认属性与默认值

- `isDefault`：控制是否自动显示在 DIY 页面；
- `defaultJson`：控制新建记录时的初始数据。

新建流程：

```text
1. 遍历当前 variant 的字段
2. 如果 field.defaultJson 非空，写入 Data[field.name]
3. 自动填充 isDefault == true 的字段到 UI
4. 用户编辑时覆盖 Data 中对应值
```

---

## 9. 引用处理

### 9.1 运行时引用仍为全局下标

当前 `Database` 不变时，运行时仍然使用：

```text
UnitData.Skills -> SkillData 全局下标
```

因此 Pack 编译器必须输出下标。

### 9.2 编辑期引用使用 Id

Pack 文件中：

```json
"Skills": ["skill_amiya_1", "skill_amiya_2"]
```

编译时：

```text
skill_amiya_1 -> SkillData 全局下标 12
skill_amiya_2 -> SkillData 全局下标 15
```

输出：

```json
"Skills": [12, 15]
```

### 9.3 DiyReferenceMap

必须补充引用字段映射：

```json
{
  "UnitData": {
    "Skills": { "target": "SkillData", "isArray": true },
    "MainSkill": { "target": "SkillData", "isArray": true }
  },
  "SkillData": {
    "Buffs": { "target": "BuffData", "isArray": true },
    "Bullet": { "target": "BulletData", "isArray": false },
    "Modifys": { "target": "ModifyData", "isArray": true }
  }
}
```

来源：

1. Excel 仍存在时，从第 3 行类型信息生成；
2. 或从旧 `Config` 类生成规则中补充；
3. 最终保存为独立 JSON，运行时和迁移工具共同使用。

### 9.4 下标安全规则

在稳定 Id 引用改造前：

1. 新记录只能追加；
2. 不允许重排；
3. 不物理删除，或删除时保留占位；
4. 保存后同步更新 `_index.txt`；
5. 编译时校验：

```text
旧 Id 顺序 == 新 Id 顺序 的前缀
新 Id 只出现在末尾
```

---

## 10. 旧配置兼容与反向生成

### 10.1 总体策略

第一阶段不迁移旧数据，只做**虚拟 Legacy Pack**。

```text
Data/UnitData/battle.txt
Data/SkillData/battle.txt
...
        ↓ LegacyPackProvider
DiyPack(PackId = battle, 内存对象)
```

这样：

- 不破坏旧数据；
- DIY 可以立即编辑旧配置；
- 后续再物理落盘为 `Packs/legacy/battle.json`。

### 10.2 反向生成流程

```text
1. 扫描 Data/<Table>/*.txt，排除 _index.txt
2. 按文件 stem 分组
   battle.txt -> PackId = battle
3. 读取每个表的 _index.txt
   index -> Id
4. 解析每条 JSON 记录
5. 根据 DiyReferenceMap 把引用下标转换为 Id
6. 按 PackId 构建记录集合
7. 生成 Legacy Pack
8. 生成迁移报告
```

### 10.3 全局表

需要扫描的表：

```text
CardData
UnitData
SkillData
BuffData
ModifyData
BulletData
EffectData
SpineData
ContractData
```

### 10.4 反向引用转换示例

旧记录：

```json
{
  "Id": "char_002_amiya",
  "Skills": [12, 15],
  "MainSkill": [15]
}
```

`_index.txt`：

```text
...
12 -> skill_a
15 -> skill_b
```

转换后：

```json
{
  "Id": "char_002_amiya",
  "Skills": ["skill_a", "skill_b"],
  "MainSkill": ["skill_b"]
}
```

### 10.5 重复 Id 处理

当前旧数据存在重复 Id（例如部分敌人的 UnitData 在不同文件中重复）。

迁移时必须：

- 生成重复报告；
- 默认保留“最后加载”或“第一个加载”策略，并显式配置；
- 不允许静默覆盖；
- 提供人工确认入口。

### 10.6 共享配置处理

第一版建议：

| 配置 | 处理 |
|---|---|
| 全局技能 | 保留原位置，标记为 shared 候选 |
| 共享 Buff/Bullet/Effect/Modify | 保留原 Pack，生成跨 Pack 引用报告 |
| SpineData | 建议迁移到 `shared/spine`，按 Model 去重 |
| ContractData | 独立 Pack |
| CardData | 可选记录；只有存在时才生成 |

### 10.7 迁移报告

迁移工具需要输出：

```text
DiyPackMigrationReport
├── packCount
├── recordCountByTable
├── duplicateIds
├── missingReferences
├── crossPackReferences
├── sharedCandidates
├── missingSpineData
├── warnings
└── errors
```

建议保存为：

```text
Assets/Bundles/Data/DIY/MigrationReport.json
```

### 10.8 虚拟 Pack 与物理 Pack

第一阶段：

```text
LegacyPackProvider -> 内存 DiyPack
```

第二阶段：

```text
Tools → DIY → 旧配置迁移为 DiyPack
```

生成：

```text
Packs/legacy/<PackId>.json
```

迁移完成后可选择：

- 继续读取旧数据；
- 切换到新 Pack 数据；
- 或进入 Pack 编译器流程。

---

## 11. 保存设计

### 11.1 保存单位

保存最小单位是 **一个 Pack**，不是单个表文件。

### 11.2 保存流程

```text
1. 校验 Schema
2. 校验 Record 必填字段
3. 校验引用
4. 校验 Id 重复
5. 生成完整 JObject
6. 写临时文件 pack.json.tmp
7. 原子替换 pack.json
8. 更新 Packs/index.json
9. 编译为运行时 Data/<Table>/<PackId>.txt
10. 更新 _index.txt
11. 重新加载当前 Pack
12. 可选 Database.Clear() + Init()
```

### 11.3 原子写入

不要直接：

```csharp
File.WriteAllText(path, json);
```

推荐：

```text
写 .tmp -> 校验 -> File.Replace / File.Move
```

### 11.4 备份

保存前：

```text
<PackId>.json.bak
```

备份数量可配置，建议保留最近 3 份。

### 11.5 编译输出

Pack 编译为当前运行时格式：

```text
Data/UnitData/<PackId>.txt
Data/SkillData/<PackId>.txt
Data/BuffData/<PackId>.txt
...
```

每行一个 JSON 对象，引用字段写成运行时下标。

---

## 12. Pack 编译器

### 12.1 输入

```text
DiyPack
+ DiyReferenceMap
+ 全局 _index
+ 当前 Database
```

### 12.2 输出

```text
Data/<Table>/<PackId>.txt
Data/<Table>/_index.txt
```

### 12.3 编译步骤

```text
1. 收集 Pack 内全部记录
2. 解析 Pack 内 Id 引用
3. 解析 dependencies 中的共享 Pack
4. 构建本次编译的全局 Id -> 下标映射
5. 把引用字段从 Id 转为下标
6. 输出 JSONL
7. 更新 _index.txt
8. 输出编译报告
```

### 12.4 编译约束

- 同一 Pack 内 Id 不能重复；
- 同一表内全局 Id 不能重复；
- 引用必须可解析；
- 不允许修改旧 Id 顺序；
- 新 Id 只能追加到末尾。

---

## 13. 错误处理与日志

统一错误类型：

```csharp
DiyDiagnostic
├── Level: Info / Warning / Error
├── Table
├── PackId
├── RecordId
├── Field
└── Message
```

输出位置：

- `m_tip`：界面短提示；
- `Debug.LogWarning`：可恢复问题；
- `Debug.LogError`：保存失败、编译失败；
- `MigrationReport.json`：迁移问题。

---

## 14. 分阶段实施计划

### 阶段 0：冻结数据结构

输出：

- `DiyPack`
- `DiyPackRecord`
- `DiyPackManifest`
- `DiyReferenceMap`
- `DiyRecordDraft`
- `DiyDiagnostic`

### 阶段 1：补全 Schema

输出：

- `DiySchemaField.referenceType`
- `DiySchemaField.isArray`
- `DiySchemaField.control`
- `DiySchemaRegistry`
- `DiyValueCodec`

### 阶段 2：Legacy Pack Provider

输出：

- 扫描旧数据；
- 按文件名分组；
- 下标 → Id；
- 虚拟 Legacy Pack；
- 迁移报告。

### 阶段 3：Pack 文件 Provider

输出：

- Pack 读写；
- Pack 索引；
- Pack 校验；
- 编辑器接入。

### 阶段 4：DIY UI 接入

输出：

- folders/jsonFiles 映射；
- Pack 加载；
- 单位列表；
- 自动属性填充；
- 值绑定。

### 阶段 5：保存与编译

输出：

- Pack 保存；
- 原子写入；
- 编译为运行时格式；
- `_index.txt` 更新；
- 热重载。

### 阶段 6：旧数据迁移

输出：

- 虚拟 Pack → 物理 Pack；
- 迁移工具；
- 迁移报告；
- 共享 Pack 策略。

### 阶段 7：验收与清理

输出：

- 完整测试；
- 移除 Excel 读取残留；
- 文档更新；
- 任务清单收尾。

---

## 15. 测试与验收

### 15.1 功能验收

- [ ] `folders` 能列出旧表目录与新 Pack 分类；
- [ ] `jsonFiles` 能列出对应文件；
- [ ] 选择旧文件能生成虚拟 Legacy Pack；
- [ ] 选择新 Pack 能加载 DiyPack；
- [ ] UnitData 页面能显示单位；
- [ ] 自动属性按 Schema 正确填充；
- [ ] 修改属性后能写回 JObject；
- [ ] 保存后能重新加载并保持一致；
- [ ] 新单位能创建并保存；
- [ ] 旧数据能反向生成 Pack；
- [ ] 迁移报告能输出重复 Id / 缺失引用。

### 15.2 数据验收

- [ ] Pack 内 Id 唯一；
- [ ] 表内全局 Id 唯一；
- [ ] 引用全部可解析；
- [ ] `_index.txt` 顺序合法；
- [ ] 旧 Id 顺序不变化；
- [ ] 新 Id 只追加；
- [ ] 空值 / 清空行为符合字段类型；
- [ ] 未知字段不会被覆盖丢失。

### 15.3 回归验收

- [ ] 旧配置仍可被 `Database` 加载；
- [ ] 旧战斗逻辑不受影响；
- [ ] JsonSkill 技能仍可初始化；
- [ ] CardData / UnitData 引用正常；
- [ ] 编辑器切换 JSON 后无残留数据；
- [ ] 保存失败不会损坏原文件。

---

## 16. 风险与缓解

| 风险 | 缓解 |
|---|---|
| 旧数据引用下标错乱 | 先只追加不重排；补 Id 引用映射 |
| 重复 Id 覆盖 | 迁移报告 + 人工确认 |
| 共享配置重复 | 引入 SharedPack |
| 保存中断损坏文件 | 临时文件 + 原子替换 + 备份 |
| Schema 与 Config 类漂移 | Schema 生成器 + 启动校验 |
| 旧数据迁移不可逆 | 不自动删除旧数据；先虚拟 Pack |
| UI 改造过大 | 复用 folders/jsonFiles |
| 属性值类型错误 | DiyValueCodec 强类型解析 |

---

## 17. 结论

推荐最终落地形态：

```text
DiySchema.json
    ↓
DiyPack（配置组）
    ↓
DiyRecordDraft（编辑缓冲 JObject）
    ↓
DiyPackValidator + DiyPackCompiler
    ↓
运行时 Data/<Table>/<PackId>.txt + _index.txt
    ↓
Database
```

核心原则：

1. **Pack 是保存和校验的最小单位。**
2. **UnitData 是 Pack 的入口，不是所有配置的唯一所有者。**
3. **编辑期使用稳定 Id，运行期编译为下标。**
4. **旧数据先做虚拟 Legacy Pack，不立即破坏原文件。**
5. **新增记录只追加，不重排，保证旧引用安全。**
6. **UI 继续复用 folders/jsonFiles，不新增大块布局。**

按本文任务清单实施，可以在不破坏现有运行时数据层的前提下，把 DIY 编辑器从 Excel 模式平滑迁移到 JSON 配置组模式。

---

## 18. 完整可执行任务清单

> 以下任务按依赖顺序排列。建议每完成一项就勾选，并在提交信息中引用阶段编号。

### 阶段 A：数据结构与基础协议

- [ ] A01 新建 `Assets/Scripts/DIY/Data/DiyPack.cs`，定义 `DiyPack`、`DiyPackRecord`、`DiyPackManifest`。
- [ ] A02 新建 `DiyRecordDraft.cs`，包含 `Original`、`Data`、`DirtyFields`、`IsNew`。
- [ ] A03 新建 `DiyDiagnostic.cs`，定义诊断级别与字段。
- [ ] A04 新建 `DiyReferenceMap.cs`，定义字段引用映射模型。
- [ ] A05 新建 `DiyPackPaths.cs`，集中管理 `Data/Packs`、`Data/<Table>`、`DiySchema.json` 路径。
- [ ] A06 定义 `IDiyPackProvider` 接口。
- [ ] A07 定义 `IDiyRecordStore` 接口。
- [ ] A08 定义 Pack JSON 的序列化/反序列化规则。
- [ ] A09 编写 Pack JSON Schema 示例，放入 `Docs` 或测试资源。
- [ ] A10 为 `DiyPack`、`DiyRecordDraft` 增加单元测试或编辑器自检。

### 阶段 B：DiySchema 扩展

- [ ] B01 扩展 `DiySchemaField`，增加 `referenceType`。
- [ ] B02 扩展 `DiySchemaField`，增加 `isArray`。
- [ ] B03 扩展 `DiySchemaField`，增加 `control`（text/bool/combo/reference）。
- [ ] B04 Schema 版本升级到 3。
- [ ] B05 修改 `DiySchemaGeneratorWindow` 输出新字段。
- [ ] B06 从旧 Excel 表头为引用字段补充 `referenceType`。
- [ ] B07 生成独立 `DiyReferenceMap.json`。
- [ ] B08 新建运行时 `DiySchemaRegistry`，加载并缓存 Schema。
- [ ] B09 `DiySchemaRegistry` 支持按 `className + variant + fieldName` 查询。
- [ ] B10 `DiySchemaRegistry` 支持取默认属性列表。
- [ ] B11 `DiySchemaRegistry` 支持取字段默认 `JToken`。
- [ ] B12 为 Schema 加载增加版本与缺字段校验。

### 阶段 C：值转换与 JObject 缓冲

- [ ] C01 新建 `DiyValueCodec`，实现 UI 字符串 → `JToken`。
- [ ] C02 实现 `JToken` → UI 字符串。
- [ ] C03 实现 `GetDefaultToken(Type)`。
- [ ] C04 覆盖 int / float / double / bool / string。
- [ ] C05 覆盖枚举与枚举数组。
- [ ] C06 覆盖 int[] / string[] / object[]。
- [ ] C07 覆盖 Vector2 / Vector2Int / Vector3 / Rect。
- [ ] C08 覆盖 Vector2[] / Vector2Int[]。
- [ ] C09 实现引用字段 Id → `JToken` 的编辑期表示。
- [ ] C10 实现清空值策略：数值 0、字符串空、数组空、bool false。
- [ ] C11 为 `DiyValueCodec` 编写单元测试。
- [ ] C12 将 `DiyValueConverter` 的默认值转换逻辑与 `DiyValueCodec` 对齐。

### 阶段 D：Legacy 旧数据兼容

- [ ] D01 新建 `LegacyPackProvider`。
- [ ] D02 扫描 `Data/<Table>/*.txt`，排除 `_index.txt`。
- [ ] D03 按文件 stem 分组，生成 PackId。
- [ ] D04 读取每张表的 `_index.txt`，建立 index → Id 映射。
- [ ] D05 解析 JSONL 与 JSON 数组。
- [ ] D06 使用 `DiyReferenceMap` 将引用下标转换为 Id。
- [ ] D07 构建虚拟 `DiyPack`。
- [ ] D08 处理 `CardData` 可选记录。
- [ ] D09 处理 `SpineData` 共享候选。
- [ ] D10 处理 `ContractData` 独立 Pack 候选。
- [ ] D11 检测重复 Id 并输出报告。
- [ ] D12 检测无法解析的引用并输出报告。
- [ ] D13 检测跨 Pack 引用并输出报告。
- [ ] D14 输出 `MigrationReport.json`。
- [ ] D15 为 LegacyPackProvider 编写测试。

### 阶段 E：Pack 文件 Provider

- [ ] E01 新建 `DiyPackFileProvider`。
- [ ] E02 读取 `Data/Packs/index.json`。
- [ ] E03 校验 PackId 唯一。
- [ ] E04 校验 records 表名合法。
- [ ] E05 校验记录 Id 唯一。
- [ ] E06 校验 entryUnits 存在。
- [ ] E07 校验 dependencies 存在。
- [ ] E08 加载单个 Pack 文件。
- [ ] E09 保存单个 Pack 文件。
- [ ] E10 实现 Pack 列表刷新。
- [ ] E11 实现 Pack 搜索/过滤。
- [ ] E12 实现 Pack 创建。
- [ ] E13 实现 Pack 复制。
- [ ] E14 实现 Pack 删除（先软删除）。
- [ ] E15 为 PackFileProvider 编写测试。

### 阶段 F：DIY UI 接入 Pack

- [ ] F01 修改 `folders` 数据源，支持旧表目录与 `Packs` 分类。
- [ ] F02 修改 `jsonFiles` 数据源，支持旧文件与 Pack 文件。
- [ ] F03 新建 `DiyPackSelection`，记录当前 PackId、来源类型、文件路径。
- [ ] F04 选择旧文件时调用 `LegacyPackProvider`。
- [ ] F05 选择新 Pack 时调用 `DiyPackFileProvider`。
- [ ] F06 统一转换为 `DiyPackView`。
- [ ] F07 刷新单位下拉框。
- [ ] F08 刷新技能/ Buff / Modify 等页面数据源。
- [ ] F09 切换 Pack 时清理旧草稿。
- [ ] F10 切换页面时保持当前 Pack。
- [ ] F11 在 `m_tip` 显示当前 PackId 与来源。
- [ ] F12 处理空 Pack、坏 Pack、缺 Schema 的提示。

### 阶段 G：属性自动填充与绑定

- [ ] G01 读取当前 variant 的 `isDefault` 字段。
- [ ] G02 通过 `m_funPage.onChanged` 统一刷新属性列表。
- [ ] G03 使用 `UI_AttributeLable` 构建属性项。
- [ ] G04 text 控件绑定。
- [ ] G05 bool 控件绑定。
- [ ] G06 combox 控件绑定枚举。
- [ ] G07 引用字段使用 Id 下拉。
- [ ] G08 属性项保存 `DiyFieldMeta` 到 `item.data`。
- [ ] G09 修改属性时写入 `DiyRecordDraft.Data`。
- [ ] G10 记录 `DirtyFields`。
- [ ] G11 增加“添加属性”下拉框。
- [ ] G12 “添加属性”排除已经自动填充的字段。
- [ ] G13 “删除属性项”不删除 JSON 字段。
- [ ] G14 增加字段分组显示。
- [ ] G15 增加字段校验错误提示。

### 阶段 H：保存与编译

- [ ] H01 实现 `DiyPackValidator`。
- [ ] H02 校验必填字段。
- [ ] H03 校验字段类型。
- [ ] H04 校验引用字段。
- [ ] H05 校验 Id 唯一。
- [ ] H06 校验 `_index` 顺序安全。
- [ ] H07 生成完整 JObject。
- [ ] H08 写入 `.tmp` 文件。
- [ ] H09 原子替换 Pack 文件。
- [ ] H10 生成 `.bak` 备份。
- [ ] H11 更新 `Packs/index.json`。
- [ ] H12 新建 `DiyPackCompiler`。
- [ ] H13 Pack → `Data/<Table>/<PackId>.txt`。
- [ ] H14 引用 Id → 运行时下标。
- [ ] H15 更新 `_index.txt`，只追加。
- [ ] H16 编译后热重载 `Database`。
- [ ] H17 保存失败回滚。
- [ ] H18 保存成功刷新 UI。
- [ ] H19 输出编译报告。
- [ ] H20 为保存流程编写集成测试。

### 阶段 I：旧数据迁移

- [ ] I01 增加菜单 `Tools → DIY → 旧配置迁移为 DiyPack`。
- [ ] I02 虚拟 Legacy Pack → 物理 Pack 文件。
- [ ] I03 生成 `Packs/legacy/<PackId>.json`。
- [ ] I04 生成 `Packs/index.json` 条目。
- [ ] I05 不删除旧数据文件。
- [ ] I06 输出迁移前后对比报告。
- [ ] I07 支持只迁移指定 Pack。
- [ ] I08 支持重复 Id 人工确认。
- [ ] I09 支持共享配置提取到 `shared` Pack。
- [ ] I10 支持 SpineData 去重。
- [ ] I11 支持 ContractData 独立 Pack。
- [ ] I12 迁移后执行一次完整加载校验。

### 阶段 J：移除 Excel 残留

- [ ] J01 移除 `UI_Main.cs` 中所有 Excel 读取调用。
- [ ] J02 移除 `attributeNames` 硬编码列表。
- [ ] J03 移除 `freshAttribute` 的 Excel 模板依赖。
- [ ] J04 移除 `save2Excel` 旧实现。
- [ ] J05 移除 `excelPath` / `unitIndexs` 的旧 Excel 语义。
- [ ] J06 保留旧 Excel 导入工具，但标记为“仅迁移使用”。
- [ ] J07 更新 `Docs/开发者文档/07-工具链与Excel导表.md`，说明 Excel 已不是运行时数据源。
- [ ] J08 更新 `Docs/开发者文档/02-启动流程与数据流.md`，补充 Pack 数据流。

### 阶段 K：测试与验收

- [ ] K01 测试 folders 列表。
- [ ] K02 测试 jsonFiles 列表。
- [ ] K03 测试旧数据虚拟 Pack。
- [ ] K04 测试新 Pack 加载。
- [ ] K05 测试 UnitData 加载。
- [ ] K06 测试 CardData 加载。
- [ ] K07 测试 SkillData 引用转换。
- [ ] K08 测试 BuffData 引用转换。
- [ ] K09 测试 ModifyData 引用转换。
- [ ] K10 测试 BulletData 引用转换。
- [ ] K11 测试 EffectData 引用转换。
- [ ] K12 测试 SpineData 共享。
- [ ] K13 测试 ContractData 独立 Pack。
- [ ] K14 测试新建单位。
- [ ] K15 测试修改单位。
- [ ] K16 测试保存后重载。
- [ ] K17 测试重复 Id 拦截。
- [ ] K18 测试缺失引用拦截。
- [ ] K19 测试 `_index` 顺序保护。
- [ ] K20 测试保存中断不损坏文件。
- [ ] K21 测试旧战斗流程无回归。
- [ ] K22 测试 JsonSkill 初始化无回归。
- [ ] K23 测试 Android 环境路径与只读限制。
- [ ] K24 输出最终验收报告。

### 阶段 L：文档与收尾

- [ ] L01 更新 `Docs/开发者文档/目录.md` 增加本文索引。
- [ ] L02 更新 DIY 编辑器使用说明。
- [ ] L03 更新 Pack 文件格式说明。
- [ ] L04 更新 Schema 字段说明。
- [ ] L05 更新迁移工具使用说明。
- [ ] L06 记录已知限制与后续计划。
- [ ] L07 清理调试日志与临时文件。
- [ ] L08 确认所有新增 `.cs` 为 UTF-8 无 BOM。
- [ ] L09 确认新增 `.meta` 已生成并提交。
- [ ] L10 完成最终 Code Review。
