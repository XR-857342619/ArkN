# 14 JsonSkill 开发者文档

> 版本：v1.1（2026-08-28 同步至 v1.6.5）
> 适用工程：`zhou-master`
> 相关文档：`12-Skill系统重构计划书.md`、`13-Skill系统详细设计方案.md`
> 本文面向需要理解或扩展 JsonSkill 框架的开发者。

---

## 1. 概述

JsonSkill 是 Skill 系统的新一代实现方式，目标是把“技能逻辑”从大量 `Skill/Types/*.cs` 子类中解放出来，改为由 **JSON 配置 + 选择器/排序器/效果器组合** 驱动。

当前实现包含：

- 新技能宿主：`JsonSkill`
- JSON 数据模型：`SkillJsonData`
- 目标选择/排序框架：`DynamicTargetSelector` / `DynamicSorter`
- 效果器框架：`ISkillEffect` / `EffectDispatcher` / `SkillEffectFactory`
- 校验器：`SkillJsonValidator`
- 参数反射绑定：`JsonConfigHelper`

旧技能系统仍然保留，`JsonSkill` 与旧 `Skill/Types` 并行运行。

---

## 2. 技术路线

```
SkillJsonData（配置）
      │
      ▼
JsonSkill : Skill（宿主）
      │
      ├── Selectors ──► DynamicTargetSelector
      │                    ├── ISelectorStrategy（主动产生候选目标）
      │                    └── IFilterStrategy（过滤候选目标）
      │
      ├── Sorters ────► DynamicTargetSelector / DynamicSorter
      │                    └── ISortStrategy（排序）
      │
      └── Effects ────► EffectDispatcher
                           └── ISkillEffect（执行效果）
```

设计原则：

1. **宿主只负责通用机制**：SP、冷却、连发、蓄力、目标缓存、攻击范围显示等继续由 `Skill` 基类提供。
2. **行为组合化**：选择器、排序器、效果器都是可插拔策略。
3. **配置 JSON 化**：一份 JSON 描述一个技能，避免 115 个字段的 `SkillData` 继续膨胀。
4. **新旧并行**：`SkillData.Type == "Json"` 或存在对应 `SkillJsonData` 时走 JsonSkill，否则走旧反射子类。

---

## 3. 目录结构

```
Assets/Scripts/Core/Skill/
├── Json/
│   ├── SkillJsonData.cs          // JSON 技能配置根对象
│   ├── SkillBaseConfig.cs        // 通用基础字段
│   ├── SelectorNode.cs           // 选择器节点
│   ├── SorterNode.cs             // 排序器节点
│   ├── EffectNode.cs             // 效果器节点
│   ├── SkillEffectTrigger.cs     // 效果触发时机枚举
│   ├── SkillJsonValidator.cs     // 配置校验
│   ├── JsonConfigHelper.cs       // JSON 参数反射绑定工具
│   └── JsonSkill.cs              // JsonSkill 宿主
├── Effects/
│   ├── ISkillEffect.cs           // 效果器接口
│   ├── SkillEffectFactory.cs     // 效果器反射工厂
│   ├── EffectDispatcher.cs       // 按 Trigger 派发效果器
│   ├── EffectUtil.cs             // 效果器公共工具
│   ├── DamageEffect.cs
│   ├── HealEffect.cs
│   ├── AddBuffEffect.cs
│   ├── RemoveBuffEffect.cs
│   ├── BulletEffect.cs
│   ├── PushEffect.cs
│   ├── PullEffect.cs
│   ├── CostEffect.cs
│   ├── AttributeModifyEffect.cs
│   ├── SkillEventEffect.cs
│   ├── SummonEffect.cs
│   └── TriggerEventEffect.cs
├── TargetSelector/
│   ├── JsonSelectors.cs          // JsonSkill 可用的主动选择器
│   ├── 主要目标选择器.cs          // 原有筛选器/选择器，已扩展支持 Json
│   └── 主要目标排序器.cs          // 原有排序器，已扩展支持 Json
├── DynamicTargetSelector.cs      // 动态选择/排序执行器 + 工厂
├── DynamicSorter.cs              // 动态排序器（保留旧接口 + Json 重载）
└── ISkillDiy.cs                  // 策略接口与 SkillContext
```

---

## 4. 核心类说明

### 4.1 `SkillJsonData`

技能配置根对象，继承 `IConfig`。

```csharp
public class SkillJsonData : IConfig
{
    public string Id { get; set; }
    public string Name;
    public string Description;
    public string Icon;

    public SkillBaseConfig Base;
    public List<SelectorNode> Selectors;
    public List<SorterNode> Sorters;
    public List<EffectNode> Effects;
}
```

### 4.2 `SkillBaseConfig`

只保存宿主通用字段，例如：

- SP/费用：`SkillCost`、`MaxPower`、`StartPower`、`PowerType`、`PowerUseType`
- 使用方式：`UseType`、`ReadyType`
- 冷却/连发：`Cooldown`、`OpenTime`、`BurstCount`、`BurstDelay`
- 目标基础：`TargetTeam`、`DeadFind`
- 范围/表现：`AttackPoints`、`AttackRange`、`ModelAnimation`、`OverwriteAnimation`

注意：`ReadyType` 在 JSON 中可直接写 `"SP"`，内部会映射为 `SkillReadyEnum.特技激活`。

### 4.3 节点类

```csharp
public class SelectorNode
{
    public string Type;
    public Dictionary<string, object> Data;
}

public class SorterNode
{
    public string Type;
    public SortDirection Direction;
    public Dictionary<string, object> Data;
}

public class EffectNode
{
    public string Type;
    public string Trigger;
    public int Priority;
    public Dictionary<string, object> Data;
}
```

### 4.4 `JsonSkill`

继承 `Skill`，是 JSON 技能的运行时宿主。

核心职责：

- 加载 `SkillJsonData`
- 把 `Base` 字段同步到占位 `SkillData`，使旧基类机制可复用
- 构造 `EffectDispatcher`
- 在生命周期钩子中派发效果：
  - `OnInit`
  - `OnStart`
  - `OnCast`
  - `OnHit`
  - `OnBreak`
  - `OnEnd`
- 使用 `DynamicTargetSelector` 完成索敌和排序

### 4.5 `EffectDispatcher`

- 根据 `EffectNode.Trigger` 分组
- 同一 Trigger 内按 `Priority` 升序执行
- 缓存效果器实例，避免高频释放时反复反射创建

### 4.6 `SkillEffectFactory`

- 启动时扫描程序集中所有 `ISkillEffect`
- 以 `Name` 为 key 注册
- 提供 `Create(string)` / `Contains(string)`

### 4.7 `TargetSelectorFactory`

- 扫描并注册：
  - `IFilterStrategy`
  - `ISortStrategy`
  - `ISelectorStrategy`
- 根据 JSON `Data` 反射绑定构造函数参数

### 4.8 `JsonConfigHelper`

- 把 `Dictionary<string, object>` 绑定到策略构造函数参数
- 支持大小写不敏感匹配
- 支持基础类型转换、数组/List 转换
- 支持 `ReadyType = "SP"` 这类特殊映射

---

## 5. 数据加载流程

`Database` 中同时加载：

```csharp
AddAsync<SkillData>("SkillData");
AddAsync<SkillJsonData>("SkillJson");
```

编辑器同步加载：

```csharp
Add<SkillData>("SkillData");
Add<SkillJsonData>("SkillJson");
```

数据文件位置：

```
Assets/Bundles/Data/SkillData.txt
Assets/Bundles/Data/SkillJson.txt
```

`SkillJson.txt` 每行一个 JSON 对象，与 `SkillData.txt` 的加载方式一致。

---

## 6. JsonSkill 运行流程

### 6.1 创建

`Unit.LearnSkill()` 中：

1. 查找 `SkillData`
2. 如果 `SkillData.Type == "Json"` 或存在对应 `SkillJsonData`，创建 `JsonSkill`
3. 设置 `skill.Unit` 和 `skill.Id`
4. 调用 `skill.Init()`

### 6.2 初始化

`JsonSkill.Init()`：

1. 通过 `SkillData.Id` 查找 `SkillJsonData`
2. 把 `Base` 字段写回占位 `SkillData`
3. 调用 `base.Init()` 初始化 SP/冷却/攻击范围等通用机制
4. `EffectDispatcher.Build()` 构建触发表
5. 派发 `OnInit`
6. 校验配置并输出警告

### 6.3 索敌

`FindTarget()` / `GetAttackTarget()`：

1. 创建 `SkillContext`
2. 调用 `DynamicTargetSelector.SelectTargets()`
3. 依次执行 `Selectors`
   - `ISelectorStrategy`：产生候选目标
   - `IFilterStrategy`：过滤候选目标
4. 执行 `Sorters` 排序

### 6.4 释放

`Cast()`：

1. 处理充能消耗
2. 按需重新索敌
3. 派发 `OnCast`
4. 调用 `SpSkillEffect()`，与旧技能钩子保持一致
5. 处理连发
6. 清空目标

---

## 7. JSON 配置编写指南

### 7.1 最小示例

```json
{
  "Id": "skill_json_001",
  "Name": "示例范围伤害",
  "Description": "对攻击范围内敌人造成物理伤害",
  "Icon": "",

  "Base": {
    "SkillCost": 0,
    "MaxPower": 0,
    "StartPower": 0,
    "PowerType": "自动",
    "UseType": "自动",
    "ReadyType": "None",
    "Cooldown": 1,
    "OpenTime": 0,
    "TargetTeam": 2,
    "DeadFind": false,
    "AttackRange": 1,
    "AttackPoints": [
      { "x": 0, "y": 0 },
      { "x": 1, "y": 0 },
      { "x": -1, "y": 0 }
    ]
  },

  "Selectors": [
    {
      "Type": "从攻击范围获取单位",
      "Data": { "Team": 2 }
    }
  ],

  "Sorters": [
    {
      "Type": "距离排序",
      "Direction": "Ascending"
    }
  ],

  "Effects": [
    {
      "Type": "伤害",
      "Trigger": "OnCast",
      "Priority": 10,
      "Data": {
        "DamageRate": 1.2,
        "DamageType": "物理"
      }
    }
  ]
}
```

### 7.2 注意：`AttackPoints` 格式

项目使用 Unity `Vector2Int` 的 Newtonsoft 转换器，必须写成对象形式：

```json
{ "x": 0, "y": 0 }
```

不能写成：

```json
[0, 0]
```

### 7.3 `Trigger` 可用写法

`EffectNode.Trigger` 支持：

- `OnInit`
- `OnStart`
- `OnCast`
- `OnAttack`
- `OnHit`
- `OnLoopStart`
- `OnLoopTick`
- `OnLoopEnd`
- `OnEnd`
- `OnBreak`
- `OnKill`
- `OnDeath`

也兼容：

- `Cast`
- `Start`
- `释放技能`
- `攻击`
- `击中`
- 等常见写法

注意：当前 `JsonSkill` 实际派发的时机主要是 `OnInit / OnStart / OnCast / OnHit / OnBreak / OnEnd`，`OnAttack / OnLoop* / OnKill / OnDeath` 目前主要在枚举和校验层支持，尚未全部接入宿主派发。

---

## 8. 内置选择器 / 排序器 / 效果器

### 8.1 主动选择器 `ISelectorStrategy`

| Type | 说明 | 常用 Data |
|---|---|---|
| `从攻击范围获取单位` | 从攻击范围/攻击点获取目标 | `Team`、`Range` |
| `获取事件目标单位` | 获取当前事件目标 | `Team` |
| `获取事件来源单位` | 获取当前事件来源 | `Team` |
| `获取所有单位` | 获取全场单位 | 无 |
| `获取随机单位` | 随机获取 N 个单位 | `Count`、`Team` |
| `获取自身阻挡单位` | 获取自身阻挡的单位 | `Team` |
| `获取被阻挡的单位` | 获取被阻挡的单位 | `Team` |
| `获取被指定单位阻挡的单位` | 获取被指定单位阻挡的单位 | `Team` |

### 8.2 筛选器 `IFilterStrategy`

| Type | 说明 | 常用 Data |
|---|---|---|
| `Buff筛选` | 按 Buff 条件筛选 | `MustHaveAnyBuffIds`、`MustNotHaveAllBuffIds` 等 |
| `常用筛选器` | 仅自己/自己以外/召唤物/仅召唤 | `TargetTeam`、`FilterEnum` |
| `距离筛选` | 距离范围/精确距离过滤 | `MinDistance`、`MaxDistance`、`ExactDistance` |

### 8.3 排序器 `ISortStrategy`

| Type | 说明 |
|---|---|
| `距离排序` | 与施法者距离 |
| `距离施法者排序` | 与施法者距离 |
| `终点距离排序` | 与终点距离 |
| `仇恨排序` | 按仇恨值 |
| `生命值排序` | 按当前生命 |
| `最大生命值排序` | 按最大生命 |
| `攻击力排序` | 按攻击力 |
| `防御力排序` | 按防御力 |

### 8.4 效果器 `ISkillEffect`

| Type | 类名 | 说明 | 常用 Data |
|---|---|---|---|
| `伤害` | `DamageEffect` | 造成伤害 | `DamageRate`、`DamageBase`、`DamageType` |
| `治疗` | `HealEffect` | 治疗目标 | `HealRate`、`HealBase` |
| `添加Buff` | `AddBuffEffect` | 添加 Buff | `BuffId`、`Duration`、`Chance` |
| `移除Buff` | `RemoveBuffEffect` | 移除 Buff | `BuffIds` / `BuffId` |
| `生成子弹` | `BulletEffect` | 发射子弹 | `BulletId`、`ShootPoint`、`BulletCount` |
| `推动` | `PushEffect` | 推动目标 | `Power`、`Direction` |
| `拉动` | `PullEffect` | 拉近目标 | `Power` |
| `费用` | `CostEffect` | 增减费用 | `CostCount` |
| `属性修改` | `AttributeModifyEffect` | 修改属性 | `ModifyId` / `BuffId` / `Attribute` |
| `触发技能` | `SkillEventEffect` | 触发其他技能 | `SkillIds` / `SkillId` |
| `召唤` | `SummonEffect` | 召唤敌人 | `UnitId`、`Count`、`Range` |
| `结算事件` | `TriggerEventEffect` | 触发全局事件 | `Event` |

---

## 9. 扩展指南

### 9.1 新增效果器

1. 在 `Assets/Scripts/Core/Skill/Effects/` 下新建类
2. 实现 `ISkillEffect`
3. 给 `Name` 返回 JSON 中使用的 Type 名
4. 无需手动注册，`SkillEffectFactory` 会自动扫描

```csharp
public class MyEffect : ISkillEffect
{
    public string Name => "我的效果";

    public void Execute(SkillContext context, EffectNode node)
    {
        // 读取 node.Data 中的参数
        // 操作 context.Caster / context.Targets
    }
}
```

### 9.2 新增选择器/排序器

1. 实现 `ISelectorStrategy` / `IFilterStrategy` / `ISortStrategy`
2. 构造函数参数名尽量与 JSON `Data` key 大小写不敏感对应
3. `TargetSelectorFactory` 会自动扫描注册

```csharp
public class 我的筛选器 : IFilterStrategy
{
    public string Name => "我的筛选器";

    private readonly int _team;

    public 我的筛选器(SkillContext context, int team)
    {
        _team = team;
    }

    public Func<Unit, bool> GetPredicate() => u =>
        (u != null) && ((_team >> u.Team) & 1) == 1;
}
```

### 9.3 推荐做法

- 效果器保持无状态，便于 `EffectDispatcher` 缓存复用
- 构造函数参数使用 `SkillContext` + 基础类型参数
- JSON `Data` 中不要放复杂对象，优先使用 int/float/bool/string/数组

---

## 10. 校验与调试

### 10.1 配置校验

`SkillJsonValidator.Validate(SkillJsonData)` 会检查：

- `Id` 是否为空
- `Selectors / Sorters / Effects` 的 `Type` 是否已注册
- `EffectNode.Trigger` 是否合法

`JsonSkill.Init()` 初始化时会自动执行校验，错误会输出 `Debug.LogWarning`。

### 10.2 常见问题排查

| 现象 | 可能原因 |
|---|---|
| JsonSkill 初始化找不到配置 | `SkillData.Id` 与 `SkillJsonData.Id` 不一致 |
| Selector 不生效 | `Type` 未注册，或 `Data` 参数名与构造函数不一致 |
| 无目标 | `Selectors` 为空时 JsonSkill 会返回空目标，必须显式配置选择器 |
| AttackPoints 解析失败 | 使用了 `[0,0]`，应改为 `{"x":0,"y":0}` |
| Trigger 不触发 | 使用了当前宿主尚未派发的 Trigger，如 `OnKill` |

---

## 11. 已知限制与注意事项

1. **JsonSkill 依赖占位 SkillData**
   `JsonSkill.Init()` 需要一份 `SkillData` 占位行，`SkillData.Id` 与 `SkillJsonData.Id` 对应。

2. **`ApplyBaseConfig` 会写回占位 SkillData**
   当前实现会修改 `Database` 中的 `SkillData` 对象，使旧基类机制无需大改即可复用。同一个技能 ID 的配置会被多次 JsonSkill 初始化重复写回，目前是幂等的，但后续建议改成独立运行时配置。

3. **触发时机尚未全覆盖**
   当前 JsonSkill 实际派发 `OnInit / OnStart / OnCast / OnHit / OnBreak / OnEnd`，其余枚举为后续扩展保留。

4. **效果器缓存要求无状态**
   `EffectDispatcher` 会缓存效果器实例，新增效果器时请勿在字段中保存单次执行状态。

5. **旧系统仍保留**
   只有配置为 Json 或存在 `SkillJsonData` 的技能才走 JsonSkill，其余旧技能不受影响。

---

## 12. 与旧 Skill 系统的关系

| 维度 | 旧 Skill/Types | JsonSkill |
|---|---|---|
| 配置 | Excel / `SkillData` 字段 | `SkillJsonData` JSON |
| 行为实现 | 继承 `Skill` 并重写方法 | 组合 Selector/Sorter/Effect |
| 扩展成本 | 新增子类、改 Excel、改读取逻辑 | 新增 JSON + 可能新增策略类 |
| 并行运行 | 是 | 是 |
| 迁移方式 | 旧技能可逐步改写为 JSON | 新技能优先使用 JSON |

---

> 本文档会随 JsonSkill 框架演进持续更新。
