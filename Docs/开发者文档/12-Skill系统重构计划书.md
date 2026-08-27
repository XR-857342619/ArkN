# 12 Skill 系统重构计划书

> 版本：v1.1（2026-08-28 同步）
> 状态：**已实施（渐进式）**。v1.5.0 起 JsonSkill 与旧 `Skill/Types` 并行运行，实施细节见 `13-Skill系统详细设计方案.md`、当前实现见 `14-JsonSkill开发者文档.md`。
> 与计划的差异：核心选择器/排序器/效果器已落地；计划中的内置 Skill JSON 编辑器尚未实现；实际目录为 `Core/Skill/Json/`、`Core/Skill/Effects/`、`Core/Skill/TargetSelector/`。
> 适用工程：`zhou-master`（ArknightR N 版分支）
> 相关文档：`03-核心战斗系统.md`、`13-Skill系统详细设计方案.md`、`14-JsonSkill开发者文档.md`

---

## 1. 背景与目标

### 1.1 现状问题

当前 Skill 系统存在以下问题：

1. `SkillData`（`Assets/Scripts/Config/SkillData.cs`）已有约 115 个字段，且随着需求增加还在持续膨胀。
2. `Skill` 基类（`Assets/Scripts/Core/Skill/Skill.cs`，约 1832 行）承担了过多职责：SP/充能、索敌、排序、伤害、子弹、Buff、表现、范围显示等耦合在一起。
3. 新增技能通常需要新增 `Skill/Types/*.cs` 子类，并 override 大量生命周期方法，扩展方式对策划和后续维护不友好。
4. 配置通过 Excel 导表，字段修改需要同步修改 Excel、`SkillData.cs`、导出工具、子类读取逻辑，链路长且容易出错。
5. 内置 DIY 编辑器目前仍以 Excel 单元格为编辑单位，不适合复杂技能组合编辑。

### 1.2 目标

- **Skill 基类只保留基础能力**：生命周期、SP/充能、冷却、连发、蓄力、目标缓存、攻击范围显示等通用机制。
- **其余功能组合化**：
  - 目标选择器（Filter/Selector）
  - 目标排序器（Sorter）
  - 效果器（Effect/Executor）
- **配置 JSON 化**：技能配置用 JSON 记录，取代/兼容现有 Excel 字段。
- **内置技能编辑器**：基于现有 DIY 编辑器扩展，编辑技能 JSON，保存后热重载。

### 1.3 非目标

- 不在本计划中重构 Buff/Bullet/Modify 系统。
- 不要求一次性删除所有旧技能子类，旧系统与新系统并行运行。
- 不做可视化节点编辑器 v2（先做表单式编辑器）。

---

## 2. 现状资产盘点

| 现有资产 | 路径 | 复用价值 |
|---|---|---|
| `ISkillDiy` | `Assets/Scripts/Core/Skill/ISkillDiy.cs` | 已有 `IFilterStrategy` / `ISortStrategy` / `IExecutor` 雏形 |
| 动态选择器 | `Assets/Scripts/Core/Skill/DynamicTargetSelector.cs` | 已实现筛选器组合执行 |
| 动态排序器 | `Assets/Scripts/Core/Skill/DynamicSorter.cs` | 已实现多级排序组合 |
| 内置选择器实现 | `Assets/Scripts/Core/Skill/TargetSelector/主要目标选择器.cs` | 多个筛选器实现可直接迁移 |
| 内置排序器实现 | `Assets/Scripts/Core/Skill/TargetSelector/主要目标排序器.cs` | 排序器实现 |
| 表达式引擎 | `Assets/Scripts/Helper/UnifiedExpressionEngine.cs` | 过滤/计算/赋值，可用于效果器参数 |
| 配置加载 | `Assets/Scripts/Database.cs` | 已按“每行 JSON”加载 `IConfig[]` |
| JSON 工具 | `Assets/Scripts/Helper/JsonHelper.cs` | Newtonsoft 封装 |
| DIY 编辑器 | `Assets/Scripts/UI/DIY/UI_Main.cs` | 技能/单位编辑入口 |
| 自动生成 UI | `Assets/Scripts/UI/AutoRelease/DIY/` | FairyGUI 生成 partial 类 |

---

## 3. 目标架构

### 3.1 总体结构

```
Skill（宿主）
 ├─ 基础生命周期：Init / Reset / Update / Start / BreakCast
 ├─ 通用状态：SP / 冷却 / 连发 / 蓄力 / 目标缓存
 ├─ 目标获取：TargetSelectorComposer
 ├─ 目标排序：TargetSorterComposer
 ├─ 效果执行：EffectDispatcher
 └─ 范围显示 / 动画表现

SkillConfigJson
 ├─ BaseConfig
 ├─ SelectorNode[]
 ├─ SorterNode[]
 └─ EffectNode[]
```

### 3.2 核心概念

#### 3.2.1 目标选择器（Selector/Filter）

- 职责：从候选集合中筛选目标。
- 接口沿用 `IFilterStrategy`：
  ```csharp
  public interface IFilterStrategy
  {
      string Name { get; }
      Func<Unit, bool> GetPredicate();
  }
  ```
- 后续可扩展 `ISelectorStrategy` 支持“主动产生候选目标”的场景（如“从事件目标获取”、“随机目标”）。

#### 3.2.2 目标排序器（Sorter）

- 职责：对筛选后的目标排序。
- 接口沿用 `ISortStrategy`：
  ```csharp
  public interface ISortStrategy
  {
      string Name { get; }
      Func<Unit, IComparable> GetKeySelector();
  }
  ```

#### 3.2.3 效果器（Effect/Executor）

- 职责：执行具体技能效果。
- 建议接口：
  ```csharp
  public interface ISkillEffect
  {
      string Name { get; }
      void Execute(SkillContext context, EffectNode node);
  }
  ```
- 触发时机统一使用现有 `TriggerEnum` 或新增 `SkillEffectTrigger`：
  ```
  Cast / Attack / Hit / Loop / Open / End / Kill / Die ...
  ```

#### 3.2.4 SkillContext

在现有 `SkillContext` 基础上扩展：

```csharp
public class SkillContext
{
    Unit Caster;
    Skill Skill;
    List<Unit> Targets;
    List<Vector3> TargetPositions;
    DamageInfo CurrentDamage;
    Dictionary<string, object> Parameters;   // 效果器共享临时数据
}
```

### 3.3 JSON Schema 设计

#### 3.3.1 顶层结构

```json
{
  "Id": "skill_json_001",
  "Name": "示例技能",
  "Description": "技能描述",
  "Icon": "图标路径",
  "Base": {
    "SkillCost": 10,
    "MaxPower": 30,
    "UseType": "自动",
    "ReadyType": "SP",
    "TargetTeam": 2,
    "AttackRange": 2,
    "AttackPoints": [[0, 0], [1, 0]]
  },
  "Selectors": [
    {
      "Type": "从攻击范围获取单位",
      "Data": { "Team": 2, "DeadFind": false }
    }
  ],
  "Sorters": [
    { "Type": "距离排序", "Direction": "Ascending" }
  ],
  "Effects": [
    {
      "Type": "伤害",
      "Trigger": "Cast",
      "Data": { "DamageRate": 1.2, "DamageType": "物理", "DamageCount": 1 }
    }
  ]
}
```

#### 3.3.2 BaseConfig 字段映射

`Base` 中的字段从现有 `SkillData` 中收敛而来，只保留通用基础字段：

| 字段 | 说明 |
|---|---|
| `Id` / `Name` / `Description` / `Icon` | 标识与展示 |
| `SkillCost` / `MaxPower` / `StartPower` / `PowerType` / `PowerUseType` | SP/充能 |
| `UseType` / `ReadyType` | 使用类型/就绪类型 |
| `Cooldown` / `OpenTime` | 冷却/开启时间 |
| `BurstCount` / `BurstDelay` | 连发 |
| `AttackPoints` / `AttackRange` / `AttackAreaWithMain` | 攻击范围 |
| `ModelAnimation` / `OverwriteAnimation` / `ShootPoint` | 表现 |
| `TargetTeam` / `DeadFind` | 目标基础过滤 |

#### 3.3.3 EffectNode 示例

| Type | 说明 | 建议 Data 字段 |
|---|---|---|
| `Damage` | 造成伤害 | `DamageRate`, `DamageType`, `DamageCount`, `DamageBase` |
| `Heal` | 治疗 | `HealRate`, `HealCount` |
| `AddBuff` | 添加 Buff | `BuffId`, `Duration`, `Chance` |
| `RemoveBuff` | 移除 Buff | `BuffId[]` |
| `Bullet` | 生成子弹 | `BulletId`, `ShootPoint` |
| `Push` / `Pull` | 位移 | `Power` |
| `Summon` | 召唤 | `UnitId`, `Pos` |
| `Cost` | 费用变化 | `CostCount` |
| `AttributeModify` | 属性修改 | `Attribute`, `Value` |
| `SkillEvent` | 触发其他技能 | `SkillId[]` |

---

## 4. 分阶段实施计划

### 阶段 1：Schema 与数据类（基础层）

**目标**：新技能 JSON 可被加载、反序列化、校验。

**任务**：
1. 新建目录 `Assets/Scripts/Core/Skill/Json/`。
2. 新增类：
   - `SkillJsonData : IConfig`
   - `SkillBaseConfig`
   - `SelectorNode`
   - `SorterNode`
   - `EffectNode`
3. 扩展 `Database`：
   - 增加 `AddAsync<SkillJsonData>("SkillJson")` 加载路径；
   - 编辑器下 `Add<SkillJsonData>("SkillJson")` 同步加载。
4. 扩展 `JsonHelper`：确保嵌套 `Dictionary<string, object>` 正常反序列化。
5. 增加 `SkillJsonValidator`：
   - 校验 `Selectors/Sorters/Effects` 的 `Type` 是否存在；
   - 校验 `Trigger` 枚举合法；
   - 校验必填字段。

**产出**：可通过配置加载 `SkillJsonData[]`。

**验收标准**：
- `Database.Instance.GetAll<SkillJsonData>()` 返回数据；
- 无效 JSON 会被校验并输出定位信息。

---

### 阶段 2：JsonSkill 宿主与调度器

**目标**：实现一个可运行的新技能宿主，能完成“索敌 + 排序 + 释放基础效果”。

**任务**：
1. 新增 `JsonSkill : Skill`：
   - `Init()` 读取 `SkillJsonData` 并初始化基础字段；
   - 将 `Selectors` / `Sorters` 转为 `DynamicTargetSelector` / `DynamicSorter` 节点；
   - 初始化 `EffectDispatcher`。
2. 新增 `EffectDispatcher`：
   - 维护 `Dictionary<TriggerEnum, List<EffectNode>>`；
   - 在 `Skill` 生命周期钩子中派发。
3. 改造 `Skill` 基类：
   - 增加 `protected virtual void DispatchEffect(TriggerEnum trigger)`；
   - 旧子类不受影响。
4. 实现反射工厂：
   - `SkillEffectFactory.Create(string type, ...)`；
   - `TargetSelectorFactory` / `SortStrategyFactory` 已存在，扩展为从 JSON 节点读取参数。

**产出**：`JsonSkill` 可创建并执行至少 3 个效果器。

**验收标准**：
- 配置一个“圆形范围伤害”JSON 技能，能在战斗中造成伤害；
- 旧 `Skill/Types` 技能运行不受影响。

---

### 阶段 3：核心效果器实现

**目标**：覆盖常见技能效果，支撑大部分旧技能迁移。

**任务**：
1. 新建 `Assets/Scripts/Core/Skill/Effects/`，实现：
   - `DamageEffect`
   - `HealEffect`
   - `AddBuffEffect`
   - `RemoveBuffEffect`
   - `BulletEffect`
   - `PushEffect` / `PullEffect`
   - `CostEffect`
   - `AttributeModifyEffect`
   - `SkillEventEffect`
2. 效果器内部统一使用 `SkillContext` 和 `DamageInfo`。
3. 支持 `Data` 字段中的表达式：如 `DamageRate = "context.Target.Hp * 0.2"`，由 `UnifiedExpressionEngine` 计算。
4. 每个效果器文件包含：
   - 类名、`Name` 属性；
   - `Execute` 实现；
   - `DefaultData` 说明注释。

**产出**：10 个左右核心效果器。

**验收标准**：
- 每个效果器都有对应的 JSON 测试技能；
- 效果器参数支持常量和表达式。

---

### 阶段 4：内置技能编辑器 v1

**目标**：在游戏内编辑技能 JSON，保存并热重载。

**任务**：
1. 扩展 `Assets/Scripts/UI/DIY`：
   - 新增技能 JSON 列表界面；
   - 基础参数编辑表单（`Base` 字段）；
   - 选择器/排序器/效果器列表（可增删改）。
2. 新增 `SkillJsonEditor` 控制器：
   - 读写 `StreamingAssets/Data/SkillJson.txt` 或热更路径；
   - 保存时调用 `SkillJsonValidator`；
   - 保存后 `Database.Clear()` + `Database.Init()`。
3. FairyGUI 扩展：
   - 在 `UIProject/Z` 中新增/修改 DIY 包；
   - 重新导出 `AutoRelease` 类。
4. 提供“从现有 SkillData 导出为 JSON 模板”的工具，降低录入成本。

**产出**：游戏内可编辑技能 JSON。

**验收标准**：
- 可新建、编辑、保存、加载一个 JSON 技能；
- 保存后无需重启即可在战斗中使用。

---

### 阶段 5：旧技能迁移与清理

**目标**：将旧 `Skill/Types` 技能逐步迁移到 JSON 技能。

**任务**：
1. 制定迁移批次：

| 批次 | 技能类型 | 说明 |
|---|---|---|
| 1 | `获得费用`、`回复技力`、`群攻`、`推`、`拉` | 结构简单，适合验证 |
| 2 | `伤害连发`、`传送`、`强制撤退`、`暂停` | 单触发逻辑 |
| 3 | `子弹属性修改`、`获取子弹` | 依赖子弹系统 |
| 4 | `持续施法`、`召唤相关`、`继承主技能` | 复杂，最后迁移 |

2. 每批迁移：
   - 编写对应 JSON 配置；
   - 建立战斗回归测试；
   - 旧子类保留但标记 `[Obsolete]`；
   - 验证通过后删除旧子类。
3. 清理 `SkillData` 字段：
   - 每迁移一个技能，标记 `SkillData` 中对应字段为可废弃；
   - 最终保留基础字段，其余迁入 JSON。

**产出**：旧技能全部迁移或明确保留 Legacy 名单。

**验收标准**：
- 旧子类数量逐步减少；
- `SkillData` 字段数显著下降；
- 战斗表现与迁移前一致。

---

## 5. 迁移兼容策略

### 5.1 新旧并行

- `SkillData.Type` 非空：走旧反射子类；
- `SkillData.Type == "Json"` 或空：走 `JsonSkill`。

### 5.2 配置双轨

- 旧技能仍读 `SkillData.txt`；
- 新技能读 `SkillJson.txt`；
- 两表可同时加载，互不影响。

### 5.3 回归保护

- 为每个迁移批次准备固定种子、固定编队的战斗回归场景；
- 记录关键指标：伤害总量、技能触发次数、目标选择结果；
- 迁移前后对比一致才允许删除旧子类。

---

## 6. 风险与应对

| 风险 | 影响 | 应对 |
|---|---|---|
| 效果器粒度过细，配置碎片化 | 配置难度上升 | 按“动作”抽象，不做字段级效果器 |
| 旧技能迁移量大 | 进度缓慢 | 分批迁移，新旧并行 |
| 执行顺序回归 | 战斗表现不一致 | 效果器增加 `Priority` / `Phase` 字段 |
| JSON 配置错误 | 运行时异常 | 编辑器保存时强校验 + 运行时兜底日志 |
| 编辑器开发成本高 | 进度滞后 | v1 先做表单式，节点编辑器后置 |
| 性能下降 | 组合反射过多 | 启动/导表时预编译，运行时只执行委托 |
| 表达式滥用 | 难调试 | 限制表达式使用范围，默认使用常量参数 |

---

## 7. 性能考量

1. **启动时预编译**：
   - 选择器/排序器/效果器在加载 JSON 时完成类型查找与实例创建；
   - 运行时只执行，不反复反射。
2. **避免热路径字符串比较**：
   - `EffectNode.Trigger` 在加载时转为枚举；
   - `Type` 字符串在加载时映射为委托/实例。
3. **复用现有缓存**：
   - `UnifiedExpressionEngine` 已有表达式缓存；
   - `DynamicTargetSelector` / `DynamicSorter` 保留现有工厂。
4. **目标列表复用**：
   - 与 P 系列性能优化结合，避免高频索敌产生 GC。

---

## 8. 里程碑与验收

| 里程碑 | 产出 | 验收标准 |
|---|---|---|
| M1 | Schema + 数据类 + Database 加载 | 可加载并校验 `SkillJsonData` |
| M2 | `JsonSkill` + `EffectDispatcher` | JSON 技能可完成索敌/排序/基础效果 |
| M3 | 10 个核心效果器 | 常用技能效果可配置 |
| M4 | 内置编辑器 v1 | 游戏内可编辑保存 JSON 技能 |
| M5 | 第一批旧技能迁移完成 | `获得费用/群攻/推/拉` 等迁移并回归通过 |

---

## 9. 后续可选演进

- 节点式技能编辑器 v2；
- 技能 JSON 热更新（Addressables + 热更目录）；
- 技能模板市场/预设；
- 效果器版本化与兼容迁移工具；
- 自动化测试覆盖选择器/排序器/效果器组合。

---

## 10. 文件规划

新增文件建议：

```
Assets/Scripts/Core/Skill/
├── Json/
│   ├── SkillJsonData.cs
│   ├── SkillBaseConfig.cs
│   ├── SelectorNode.cs
│   ├── SorterNode.cs
│   ├── EffectNode.cs
│   ├── SkillJsonValidator.cs
│   └── JsonSkill.cs
├── Effects/
│   ├── ISkillEffect.cs
│   ├── SkillEffectFactory.cs
│   ├── EffectDispatcher.cs
│   ├── DamageEffect.cs
│   ├── HealEffect.cs
│   ├── AddBuffEffect.cs
│   ├── RemoveBuffEffect.cs
│   ├── BulletEffect.cs
│   ├── PushEffect.cs
│   ├── PullEffect.cs
│   ├── CostEffect.cs
│   ├── AttributeModifyEffect.cs
│   └── SkillEventEffect.cs
└── Editor/
    └── SkillJsonEditor.cs
```

---

## 11. 决策记录

- **采用渐进式重构**，不推倒重写。
- **新旧系统并行**，旧 `Skill/Types` 作为 Legacy 保留。
- **效果器粒度 = 动作级**，不做字段级。
- **JSON 与 Excel 双轨运行**，新技能优先使用 JSON。
- **编辑器 v1 为表单式**，节点式后置。

---

> 本计划书用于后续实施参考，具体类名与文件路径可在实施中微调。
