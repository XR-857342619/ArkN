# 项目文档：Core 核心战斗系统（Skill / Buff / Unit）

> 适用工程：`zhou-master`（ArknightR N 版分支，明日方舟同人模拟器）
> 文档范围：`Assets/Scripts/Core` 全部代码，重点详述 **Skill（技能）**、**Buff（状态）**、**Unit（单位）** 三大子系统
> 本文档基于对 `Assets/Scripts/Core` 下 187 个 C# 文件的通读整理

---

## 目录

1. [项目概览](#1-项目概览)
2. [总体架构与数据驱动](#2-总体架构与数据驱动)
3. [Core 框架层](#3-core-框架层)
4. [Unit 单位系统](#4-unit-单位系统)
5. [Skill 技能系统](#5-skill-技能系统)
6. [Buff 状态系统](#6-buff-状态系统)
7. [子弹 / 修饰器 / 地图 / 表现 子系统](#7-子弹--修饰器--地图--表现-子系统)
8. [扩展指南：如何新增技能 / Buff / 单位](#8-扩展指南如何新增技能--buff--单位)
9. [已知问题与遗留代码](#9-已知问题与遗留代码)

---

## 1. 项目概览

### 1.1 项目背景

- 本项目是 ArknightR（独钓寒江翎 开发的明日方舟模拟器）的 N 版分支，由 X2r 与 HJsama 维护。
- 使用"互联网获得素材"自制，总体还原度较高，支持玩家地编、自建关卡（`Tools` 下有 `攻击范围编辑器.exe`、`dungeon.xlsx` 等）。
- 引擎：Unity（2019+ 风格 API），使用 Spine 骨骼动画（`Spine.Unity`）、FairyGUI（UI）、A* Pathfinding 插件（工程内 `AstarPathfindingProject`）、Addressables 资源加载。
- 代码结构：全部战斗逻辑为 **纯 C# 非 MonoBehaviour 类**（`Unit` / `Skill` / `Buff` / `Battle` 均不继承 MonoBehaviour），由少数 MonoBehaviour 门面（`BattleManager`、`MapManager` 等）驱动固定步长更新。

### 1.2 目录结构

```
Assets/Scripts/
├── Client/      客户端入口与全局管理（BattleManager 等单例门面）
├── Config/      配置数据类（SkillData / UnitData / BuffData ... 实现 IConfig）
├── Core/        核心战斗逻辑（本文档主体）
│   ├── Battle.cs        战斗容器：波次、费用、单位注册、索敌查询、事件广播
│   ├── Unit/            Unit 基类 + Units.干员/敌人/普通单位/中立单位
│   ├── Skill/           Skill 基类 + Skills.* 技能实现（Types 目录）
│   ├── Buff/            Buff 基类 + Buffs.* 状态实现（Types 目录）
│   ├── Bullet/          弹道基类 + Bullets.* 弹道实现
│   ├── Modify/          修饰器框架（Modify 基类 + Modifys.* 实现）
│   ├── Map/             地图 / 地块 / A* 寻路
│   ├── View/            表现层（模型、特效挂点、战斗相机、UI 桥接）
│   ├── Effect/          特效（对象池）
│   ├── Enum/            全部核心枚举
│   ├── DamageInfo.cs    伤害结算数据载体
│   ├── TriggerData.cs   事件参数（栈式上下文）
│   ├── CountDown.cs     倒计时工具
│   ├── SystemConfig.cs  固定步长与全局常量
│   └── ...
├── Dungeon/    肉鸽模式
├── Helper/     通用工具（JsonHelper / ResHelper / SaveHelper / Log ...）
├── MapBuild/   地图编辑器
├── Tool/       工具（Excel 导出等）
├── UI/         FairyGUI 界面
└── VFX/        特效
```

### 1.3 三个核心设计取向

1. **数据驱动 + 反射实例化**：所有单位/技能/Buff/子弹/修饰器的"类型"都由配置（Excel 导出的 JSON）里的 `Type` 字符串指定，运行时通过 `typeof(X).Assembly.CreateInstance("Skills." + type)` 创建实例。**新增一种玩法不需要改框架，只需写一个子类并在数据表里填 Type**。
2. **固定步长模拟**：`SystemConfig.DeltaTime = 1/60f`。`BattleManager.Update()` 按真实流逝时间补齐调用 `Battle.Update()` 的帧数，保证战斗模拟确定性（配合种子随机）。
3. **事件上下文栈**：`Battle.TriggerDatas` 是一个 `Stack<TriggerData>`，广播 `TriggerEnum` 事件时压入 `User / Target / Skill / Count` 上下文，技能/被动可读取"当前事件是谁、打谁、用什么技能"。

---

## 2. 总体架构与数据驱动

### 2.1 数据加载（`Assets/Scripts/Database.cs`）

- `Database` 是全局单例，把每类配置（`IConfig[]`）按 Excel 导出的 `Assets/Data/*.txt`（每行一个 JSON 对象）加载进 `Dictionary<Type, IConfig[]>`。
- 编辑器下 `Init1()` 同步读本地文件；运行时 `Init()` 走 Addressables。
- 全局技能：`SkillData.Type == "全局技能"` 的条目会被收集进 `Database.globalSkills`，每个单位 `Unit.Init()` 时都会追加学习（实现"全体生效规则"）。

### 2.2 配置数据类（`Assets/Scripts/Config/`）

| 配置类 | 作用 |
|---|---|
| `UnitData` | 单位静态数据：基础属性（Hp/Attack/Defence/MagicDefence/Speed/Agi/AttackGap/Weight...）、模型/动画名、`Skills`（技能 id 列表）、`MainSkill`（主技能列表）、Team、职业 `Profession`、`Type`（运行时反射类名）、`CanSetPos`（可部署地形标签）、`IgnoreBuff`、`Tags` 等 |
| `SkillData` | 技能静态数据（详见 §5.2，115 个字段） |
| `BuffData` | 状态静态数据：`Type`、持续时间 `LastTime`、`Upgrade`（升级成哪个 buff）、`RelyBuff`（依赖 buff）、`Resist`（是否受抵抗影响）、`RoundNeed/StopNeed/StopLess`（生效条件）、`Data`（扩展字典） |
| `BulletData` | 弹道数据：`Type`、`Model`、`Modifys`（弹道修饰器）、`EffectBase` 等 |
| `ModifyData` | 修饰器数据：`Type` + `Data` |
| `EffectData` | 特效数据：绑定骨骼点、跟随方式、朝向等 |
| `ContractData` | 合约（危机合约词条）：修改地图 HP、团队上限、追加技能 |
| `CardData` / `RelicData` / `DungeonLevelData` / `EventData` / `RewardData` / `SystemData` | 编队卡、肉鸽遗物、关卡、事件等 |

### 2.3 命名空间约定（反射的关键）

| 配置 Type 字符串 | 实际类命名空间 | 工厂调用点 |
|---|---|---|
| 单位：`干员` / `敌人` / `普通单位` / `中立单位` | `Units.` | `Battle.CreatePlayerUnit / CreateEnemy / CreateSceneUnit` |
| 技能：`普通技能` / `全局技能` / ... | `Skills.` | `Unit.LearnSkill` |
| 状态：`中毒` / `眩晕` / ... | `Buffs.` | `Unit.AddBuff`、`Bullet.AddBuff` |
| 弹道：`子弹` / `钩子` / ... | `Bullets.` | `Battle.CreateBullet` |
| 修饰器：`暴击` / `穿甲` / ... | `Modifys.` | `ModifyManager.Get` |

> ⚠️ 代码中出现 `nameof(Skills)` / `nameof(Buffs)` / `nameof(Units)` / `nameof(Bullets)` / `nameof(Modifys)` 的 `CreateInstance`，就是这些反射工厂。若新增子类不在对应命名空间，会创建失败并弹 Tip。

---

## 3. Core 框架层

### 3.1 战斗容器 `Battle`（`Battle.cs`）

职责：

- **持有战场状态**：`Cost`（费用）、`Hp/Hurt`（关卡生命）、`Tick`（帧计数）、`Waves / CheckPointWaves`（波次队列）、`SceneUnits`（场景单位）、`PlayerUnits / PlayerUnits2 / Enemys / AllUnits`、`UnitMap[,]`（敌人快速检索缓存）、`Bullets`、`Random`（种子随机）、`TriggerDatas`（事件栈）。
- **初始化** `Init(BattleInput)`：读地图 → 应用合约（`ContractData`）→ 创建玩家编队 → 创建"箱子" → 广播 `起始` → 为每名干员广播 `出场` → 把地图波次展开成 `OneWave` 时间轴（支持 `CheckPoint` 波次）。
- **主循环** `Update()`（固定 60Hz）：判定胜负 → `Tick++` → 场景单位入场 → 重建 `UnitMap` → 费用自然回复 → 刷波（普通 + CheckPoint）→ 更新地块 → 更新子弹 → 依次 `UpdatePush / UpdateBuffs / UpdateAction / UpdateCollision` → 清理死亡敌人。
- **单位工厂**：`CreatePlayerUnit(ICard, skill)` / `CreatePlayerUnit(int)` / `CreateEnemy(WaveInfo)` / `CreateSceneUnit(...)` / `CreateBullet(...)`。
- **索敌查询**：`FindAll(Vector2Int, team)`（地块/UnitMap 检索）、`FindAll(List<Vector2Int>, team)`（多点）、`FindAll(Vector2, radius, team)`（范围圆）。`team` 是位掩码：bit0=玩家、bit1=敌人、bit2=中立。
- **事件广播**：`Trigger(TriggerEnum)` → 遍历 `AllUnits` 调用 `unit.Trigger(...)`，并先触发 `RuleUnit`（规则单位，用于合约技能等全局被动）。
- **胜负**：`Hp <= 0` 失败；`EnemyCount == 0` 胜利。

### 3.2 事件系统（`TriggerEnum` + `TriggerData`）

```csharp
public enum TriggerEnum { 无, 起始, 出场, 入场, 自己入场, 落地, 离场, 攻击, 被击, 被治疗, 治疗,
    致命, 击杀, 死亡, 释放技能, 技能结束, 闪避, 击中, 阻挡, 撤退, 弹道命中,
    元素爆发, 自身元素爆发, 到达路径终点, 打数溢出, 过量治疗 }
```

- `TriggerData { Unit Target; Unit User; Skill Skill; float Count; }`。
- 广播流程（以"攻击"为例，见 `Skill.OnAttack`）：

```csharp
Battle.TriggerDatas.Push(new TriggerData { Target = target, Skill = this });
Unit.Trigger(TriggerEnum.攻击);   // 触发 Unit 自身技能的 Trigger 字段匹配
Battle.TriggerDatas.Pop();         // 事件上下文出栈
```

- `Battle.Trigger(TriggerEnum)` 是全局广播（如 `起始/死亡/元素爆发`），`Unit.Trigger` 只触发自身 `Skills` 中 `SkillData.Trigger == 枚举` 的技能（另含"受击回技力"的隐式处理）。
- 技能可通过 `SkillData.UseEventUser / UseEventTarget` 把**当前事件栈顶的 User/Target** 作为索敌来源（实现"对攻击者反击""对死亡者生效"等）。

### 3.3 计时器 `CountDown`（`CountDown.cs`）

全工程最常用的工具类：`value` 递减到 0 即 `Finished()`；`Update(dt)` 返回"是否刚结束"。技能/状态/Buff 的所有时序（冷却、抬手、持续、间隔）都由它表达。

### 3.4 伤害结算载体 `DamageInfo`（`DamageInfo.cs`）

```csharp
class DamageInfo {
    Unit Target; int AllCount; object Source;   // Skill 或 Buff
    float Attack; DamageTypeEnum DamageType; float DamageRate;
    float FinalDamage;                           // 结算后的最终伤害
    float DefIgnore; float DefIgnoreRate;        // 无视防御
    bool Avoid;                                  // 闪避/格挡
    bool Block;
    float MinDamageRate;                         // 抛光系数（默认 0.05）
}
```

- `DamageTypeEnum { Normal, Magic, Real, Element, general, Heal, LoseHP }`。
- 伤害结算入口：`Unit.Damage(DamageInfo)`（§4.3）。

### 3.5 特效 `Effect` 与对象池

- `Effect : MonoBehaviour`，由 `EffectManager` 对象池管理；`Init(Unit user, Unit target, Vector3 pos, Vector2 dir, float speed)` 按 `EffectData` 决定绑定骨骼（`BoneFollower`）、跟随方式（`ParentFollow 1/2/3`）、朝向。
- 技能/状态通过 `SkillData.StartEffect/CastEffect/HitEffect/EffectEffect/GatherEffect/LoopStartEffect/LoopCastEffect` 与 `BuffData.LastingEffect` 引用特效 id。

### 3.6 模拟预览 `Preview`（`Preview.cs`）

地图编辑器使用的轻量敌人移动预览：不创建 `Unit` 逻辑对象，仅按 `WaveInfo.Path` 模拟敌人在路径点上的移动/隐藏移动，用于验证波次路径。

---

## 4. Unit 单位系统

### 4.1 `Unit` 基类（`Unit/Unit.cs`，1232 行）

所有单位的基类（纯 C#，不继承 MonoBehaviour）。关键职责：

#### (1) 属性体系 —— 四段式重算

```csharp
// 以攻击为例：Base（基础）+ Add（加值）→ Rate（百分比）→ AddFin/Fin（最终加值/最终百分比）
Attack = ((AttackBase + AttackAdd) * (1 + AttackRate) + AttackAddFin) * (1 + AttackRateFin);
```

- `Refresh()`（带 `isRefreshing` 防重入）先清零全部 Add/Rate 修正，再遍历 `Buffs` 调用 `buff.ApplyToUnit()` 累加修正，最后统一算出最终值：`MaxHp / Hp / Attack / Defence / MagicDefence / Agi / Speed / Weight / AttackGap / SkillCost / PowerSpeed / Resist / AttackRange / StopCount ...`。
- **Buff 改属性不直接改 Base，而是改 Add/Rate 字段，再触发 `Refresh()`**——这是"数值变化"Buff 的通用通道。
- `baseAttributeInit()` 读 `UnitData` 并应用地图覆盖（`MapData.UnitOvDatas`，可对某单位做关卡数值修正）。

#### (2) 单位身份与关系

- `Id`（索引）、`Team`（0 玩家 / 1 敌人 / 2 中立）、`Profession`（职业）。
- `Parent` / `Children`：召唤物父子关系（击杀、离场联动）。
- `Battle` 反向引用。

#### (3) 状态与动作

- `StateEnum { Move, Die, Attack, Idle, Default, Start, Stun }`；`SetStatus` 会同步切换 Spine 动画名（支持 `OverWriteAnimation/Idle/Move/Die` 覆盖，技能开启时可用覆盖动画）。
- `IfAlive / IfSleep / IfSelectable / CanBeHeal / HealOnly / CanAttack / CanStopOther / IfHide / IfHideAnti`：大量布尔状态，`UpdateBuffs()` 每帧先复位再由 Buff 置位（**Buff 每帧声明式重写状态**）。
- `ScaleX / TargetScaleX`：转身插值。

#### (4) Buff / 技能挂载

- `List<Buff> Buffs`、`List<IDamageRewrite> DamageRewrites`（伤害重写按 `OrderCode` 排序）、`IgnoreBuffs`（免疫列表）。
- `List<Skill> Skills`、`MainSkill`（主动技）、`FirstSkill`（普攻，Skills[0]）、`ElementOutBreak`（元素爆发技能）。

#### (5) 技能学习 `LearnSkill(int skillId, Skill parent)`

- 反射创建 `Skills.<Type>` → `Init()` → 若 `SkillData.Skills`（附带技能）/`ExSkills`（附加技能）递归学习并挂 `Parent` 关系 → 按 Id 有序插入。
- **技能树**：一个技能可以带子技能（`Skills`）、附加技能（`ExSkills`），父技能释放时按权重触发子技能（`CastExSkill`）。

#### (6) 每帧更新入口

`Battle.Update()` 按固定步长依次调用全单位：

1. `UpdatePush()`：推拉力合算 → 失衡位移（§4.5）。
2. `UpdateBuffs()`：元素损伤结算 → Buff 逐帧 `Update()` → `Refresh()` → `UpdateView()`。
3. `UpdateAction()`：HP 自然回复 → 状态机推进（各子类重写）。
4. `UpdateCollision()`：地图边界与远程格"高地"四向落位修正（敌人）。

`UpdateSkills()`：推进 `AttackingAction`（攻击动画时长）→ 各技能 `UpdateCooldown/Update/UpdateOpening` → 攻击动画结束回 `Idle`。

#### (7) 死亡与离场

- `DoDie(source)`：`IfAlive=false` → `Dying` 倒计时（播 Die 动画）→ 广播 `击杀`（来源侧）/`死亡`（全局）→ `Finish()`。
- `Finish(leaveEvent)`：解除阻挡 → 广播 `离场` → 结束 Buff（`BuffData.DeadRemain` 的除外）→ `skill.Finish()`。

#### (8) 阻挡系统

- `StopUnits`（干员阻挡列表）、`StopCount`（阻挡数）、`敌人.StopUnit`（被谁阻挡）、`StopCost`（占用阻挡数）。
- `CanStop(enemy)`：高度相同、`CanStopOther`、数量上限 `StopCount` 校验。
- `AddStop / RemoveStop`：双向注册 + 位置吸附（半径贴合）。

#### (9) 元素损伤

- `EleInjures`（按元素类型累计伤害值）、`InjurePoint`（最大值，用于索敌排序）、`ElementProtect`（爆发保护 CD）。
- `updateElement()`：任一元素累计 ≥1000 → 触发 `元素爆发 / 自身元素爆发` 事件，匹配 `ElementOutBreak` 中 `ElementType` 相同的技能执行，随后清零该元素值。

#### (10) 仇恨 `Hatred()`

`Hatred() = -Hatre * 100000`，作为索敌排序的附加键（`Skill.GetSortOrder2` 末尾 `+ x.Hatred()`），实现"仇恨值优先"。

#### (11) 治疗 `Heal(DamageInfo, bool ifShowHeal)`

`FinalDamage = Attack * DamageRate * (1 + HealReceiveRate)` → 加 HP → 溢出时触发 `过量治疗` → 触发 `被治疗`。

### 4.2 `Unit` 子类

#### 干员 `干员`（`Unit/Types/干员.cs`）—— 玩家方

- **部署**：`CanBuild()`（费用 + 部署位 + 再部署 CD）→ `JoinMap()`：扣费、扣部署位、播 `Start` 入场动画、`InputTime = Battle.Tick`、注册地块与 UI、广播 `入场`+`自己入场`、刷新其余同名干员再部署 CD。
- **方向**：`Direction_E` + `ResetAttackPoint()`（左右/上下翻转攻击范围）；`PointWithDirection` 按方向旋转攻击点（上下方向旋转 90°）。
- **撤退**：`LeaveMap(bool recoverPower)`：广播 `撤退` → `Finish(true)`（返还部分费用 `LeaveReturn`）。
- **再部署**：`Reseting` 倒计时；同名干员离场时 `Reseting.Set(ResetTime)`。
- **费用**：`Cost`（随部署次数递增 `CostAdd`）；`GetCost()` 计算实际部署费用。
- **升级**：技能 `StartId` 记录升级前 id，离场时 `DoUpgrade(StartId)` 还原。
- **阻挡**：`IfStoped()` = `StopUnits.Count > 0`；`Refresh()` 里按 `StopCount` 上限挤掉超限敌人。
- **Alive()**：额外要求 `InputTime > 0`（未上场不算存活）。

#### 敌人 `敌人`（`Unit/Types/敌人.cs`）—— 敌方

- **路径**：出生时读 `WaveData.Path` 对应 `MapInfo.PathInfos` 的路径点序列 `PathPoints`（支持 `CheckPoint` 检查点、`Delay` 停留、`HideMove` 隐藏移动）。
- **寻路**：点对点用 `AStarPathFinder.FindPath(Map.Tiles, start, end, fly)`（A* 插件，`Map/PathFinder_AStar.cs`），带 `OffsetX/OffetsetY` 偏移；飞行敌人（`Height > 0`）直飞不寻路。
- **移动**：`UpdateMove()`：路径等待 → `CheckArrive()` 到达判定 → 临时路径重算（`NeedResetPath` 因推拉等外力触发）→ 沿 `TempPath` 移动；被阻挡/失衡/隐藏时不移动。
- **阻挡判定** `CheckBlock()`：每帧找最近的合法阻挡者 `AddStop(this)`，触发 `阻挡` 事件。
- **临时路径点**：`AddTmpPathPoint(pos, time)`（技能"尝试插入临时路径点"用）插入临时的 CheckPoint，计时结束后移除。
- **跳跃** `Jump(distance)`：沿路径快速位移（技能"跳跃"用）。
- **破门**：到达路径终点 → `Battle.DoDamage(UnitData.Damage)`（扣关卡生命）→ 触发 `到达路径终点` → `Finish`。
- **死亡计数**：`Finish()` 时 `Battle.EnemyCount--`、`Battle.CheckPoints.Add(Tick)`（用于 CheckPoint 波次解锁）。
- **距离终点**：`distanceToFinal()` 带缓存（`distanceChenged`），供"终点距离"索敌排序。

#### 普通单位 `普通单位` —— 无特殊行为的最小实现

#### 中立单位 `中立单位` —— 中立阵营（如箱子、可破坏物）

- `Init()` 学习主技能并创建 UI；`Finish()` 从各列表移除并销毁模型。

### 4.3 伤害结算流程 `Unit.Damage`（重点）

```csharp
void Damage(DamageInfo dmg) {
    damage = dmg.Attack * dmg.DamageRate;
    // 1. 受击类型易伤/减伤系数（Normal/Magic/Element 各自的 DamageReceiveRate）
    // 2. 破甲：damageWithDefence()
    //    物理：max(damage*MinDamageRate, damage - (Defence*(1-defIgnoreRate)-defIgnore))
    //    法术：max(damage*MinDamageRate, damage*(100 - 魔抗)/100)
    // 3. 全伤加成：FinalDamage = damage * (1 + DamageReceiveRate)
    // 4. 暴击表现：damage > 1.5 * 基础伤害 → ShowCrit
    // 5. 闪避/格挡判定：AllBlock/Block(物理)/MagBlock(法术) 随机 → dmg.Avoid
    // 6. 未闪避：按 OrderCode 顺序执行 DamageRewrites（护盾/屏障/锁血/减伤/分摊...）
    // 7. Hp -= FinalDamage；Hp<=0 触发「致命」事件，仍<=0 → DoDie(dmg)
    // 8. 伤害统计（干员来源计入 OpDamageInfo）
}
```

### 4.4 推拉（位移）系统

- `IPushBuff` 接口（`Buff/Types/IPushBuff.cs`）：`GetPushPower()` 返回力度向量、`Update()` 每帧刷新。
- `Unit.PushBuffs` 收集推拉 Buff；`AddPush` 使单位进入失衡（`unbalance = true`、`Unbalancing.Set(0.1f)`、`BreakAllCast()` 打断施法）。
- `UpdatePush()`：合力 → 若 `Unbalance` 按力位移；力消失且硬直结束 → `RecoverBalance()`（敌人会 `NeedResetPath = true` 重寻路）。
- 失衡会强制 `IfStun = true`（`UpdateBuffs` 中 `if (unbalance) IfStun = true`）。

### 4.5 单位创建工厂总览

| 工厂 | 创建什么 | 注册到 |
|---|---|---|
| `Battle.CreatePlayerUnit(ICard, skill)` | 编队干员 | PlayerUnits / AllUnits |
| `Battle.CreatePlayerUnit(int id)` | 箱子、召唤干员 | PlayerUnits / AllUnits |
| `Battle.CreateEnemy(WaveInfo)` | 敌人（含召唤怪） | Enemys / AllUnits / (Team0 时 PlayerUnits2) |
| `Battle.CreateSceneUnit(id, pos, dir, lifeTime)` | 场景单位（中立） | AllUnits / Tile.Units |
| `Unit.GainChild(id, mainSkillId)` | 干员召唤物（子单位） | Children + PlayerUnits |

---

## 5. Skill 技能系统

### 5.1 `Skill` 基类（`Skill/Skill.cs`，1829 行）

技能是"单位的行为剧本"：索敌 → 抬手 → 生效 → 冷却，全部由数据驱动，特殊行为通过子类重写。

#### (1) 核心字段

| 字段 | 说明 |
|---|---|
| `Unit` / `Parent` | 所属单位 / 父技能（技能树） |
| `SkillData`（由 `Id` 查配置） | 全部技能配置 |
| `Modifies` | 本技能挂载的修饰器（`ModifyManager.Get` 反射创建） |
| `Targets` / `LastTargets` | 本次索敌结果 / 连发锁定目标 |
| `AttackPoints` / `ExAttackPoints` | 攻击范围点集（按方向旋转后）；`ExAttackPoints` 可由 Buff/其他技能追加 |
| `Cooldown` / `Casting` / `Opening` / `Bursting` / `BurstGap` / `LoopingStart` / `LoopingEnd` / `Waiting` | 各阶段计时器 |
| `Power` / `MaxPower` / `PowerCount` | 技力（充能）：当前值 / 上限（`Unit.SkillCost * MaxPowerBase`）/ 可存储层数 |
| `BurstCount` / `IsBursting` | 连发计数 / 连发状态 |
| `IsNormalAttack` | 判定为普攻（自动、无充能、有动作、有伤害率） |
| `IsCantCast` / `IsCantCastCount` / `IsCantOpen` / `IsCantUse` / `IsCantBurst` / `IsCantLoop` | 各类禁用标记（被 Buff 设置） |
| `tempEvaluator` | 技能条件表达式引擎（`UnifiedExpressionEngine`，见 §5.1(6)） |

#### (2) 技能生命周期（状态机）

```
Ready() 可用
   │  (自动触发 / 玩家操作 / 事件触发)
   ▼
Start() 抬手：索敌、转身、播动画、算抬手时长
   │  Casting 倒计时
   ▼
Cast() 生效：结算目标/区域伤害、加Buff、连发启动、附加技能
   │  Cooldown 冷却
   ▼
（等待下一次 Ready）
```

- **`Ready()`**：循环/眩晕检查 → 按 `ReadyType`（`None / 特技激活 / 充能释放 / 禁止主动 / 未攻击`）与 `UseType`（`自动 / 手动 / 被动`）判定 → `EffectiveRate` 概率 → `Cooldown.Finished()`。
- **`Start()`**：`Useable()` 校验（次数上限、缴械、CD、buff 开关、StopBreak）→ 无目标且不允许无目标则返回 → `UseCount++` → 按 `ModelAnimation` 计算抬手时长（攻速 `1/Agi*100` 影响冷却；动画时长与攻击间隔互相钳制）→ `Casting.Set` → `StartEffect`。
- **`Cast()`**：充能释放类扣充能 → 普攻给全技能回技力 → 按目标/攻击点结算 `Effect` → `CastExSkill()` 触发附加技能（有 `ExSkillWeight` 则随机）→ 连发（`BurstCount > 0` 启动 `BurstGap` 节奏）→ `CastEffect` → 清空 `Targets`。
- **`BreakCast()`**：打断抬手/连发/充能开启，恢复 `Idle`。

#### (3) 索敌管线（`GetAttackTarget` → `orderTargets`）

```
GetAttackTarget()
 ├─ 事件目标：UseEventUser / UseEventTarget → 取 TriggerDatas 栈顶
 ├─ 攻击范围：AttackPoints 点集 或 AttackRange 圆（× Unit.AttackRange）
 ├─ 表达式过滤：SkillCondition（UnifiedExpressionEngine，仅 Casting 完成时）
 └─ orderTargets()
     ├─ CanUseTo 全量合法性过滤（见下）
     ├─ SortTarget：Sort(GetSortOrder1).ThenBy(GetSortOrder2).ThenBy(GetSortOrder3)
     │    Order1 = AttackOrder2（近身/飞行/远程/召唤物...）
     │    Order2 = AttackOrder（终点距离/血量/放置时间/重量/随机...） + Hatred()
     │    Order3 = OrderExpression 表达式 + OrderTag/- + OrderBuff/-
     └─ FilterTarget：按 DamageCount 截取目标数（含修饰器 ITargetModify 加成），
                      不足时触发「打数溢出」事件
```

- **`CanUseTo(target)`**：目标合法性总闸——团队位掩码、治疗对象检查、睡眠/不可选中、隐身、召唤物过滤（`TargetFilter`）、血量阈值（`SelfHpLess/TargetHpLess/TargetHpMore`）、单位/职业/稀有度/费用/位置限制、buff 开关、`AttackFly`（飞行目标）、死亡目标（`DeadFind`）、`StopAttackOnly` 阻挡者限定等。

#### (4) 伤害管线

```
Effect(target)  ── Bullet 为空 → Hit(target)    ；有 Bullet → Battle.CreateBullet
Effect(pos)     ── Bullet 为空 → Hit(pos)       ；有 Bullet → 射向指定坐标

Hit → 命中特效 → OnAttack 事件
   ├─ DamageRate > 0：doDamage
   │    ├─ AreaRange ≠ 0        → 范围圆结算 AttackArea（主目标 AreaMainDamage，其余 AreaDamage）
   │    ├─ AreaPoints ≠ null    → 按相对点结算 AttackAreaPoints
   │    └─ 单体               → Attack(target)
   │         ├─ addSkillEffect：元素损伤 + addBuff（含 BuffChance 概率）+ IUnitModify 修饰器
   │         ├─ GetDamageInfo：DamageRate 组合（×SkillData.DamageRate ×弹道Attack），
   │         │                  DamageBase 选攻击基数（自身攻击/目标MaxHp/固定/主子攻击），
   │         │                  依次过 自身buff(ISelfDamageModify) → 目标buff(IDamageModify) → 技能修饰器(IDamageModify)
   │         ├─ IfHeal → target.Heal（治疗路线）else target.Damage
   │         ├─ afterDamage：闪避事件 / 吸血 DoLifeSteal / 被击事件 / 击中事件
   │         └─ 弹道命中事件（Bullet 非空时）
   └─ DamageRate ≤ 0：仅 addSkillEffect（纯加状态技能）
```

#### (5) 技力（充能）系统

- 回复方式 `PowerType`（`PowerRecoverTypeEnum { 自动, 攻击, 受击, 闪避, 无 }`）：
  - 自动：`Update()` 里按 `Unit.PowerSpeed` 回复（受 `BattleManager.RecoverPowervSpeed` 倍率）。
  - 攻击/受击/闪避：在 `Skill.Cast`（普攻命中全技能回 1）、`Unit.Trigger`（受击）、`OnBeAvoid`（闪避）中 `RecoverPower(1)`。
- 消耗方式 `PowerUseType`：普攻类（攻击）在 `Cast` 时 `UpdateOpening`；`RecoverPower` 在开启期间（`Opening` 未结束）不回复。
- `CanOpen()`：费用足够、开启中不可重复开、充能足够 → `DoOpen()`：扣费、`Opening.Set(OpenTime)`、播覆盖动画（`OverwriteAnimation`，带 `LoopStartEffect/LoopCastEffect` 循环特效）→ `OnSkillOpen` 事件（`释放技能`）。

#### (6) 技能条件表达式 `SkillCondition` / `OrderExpression`

- 通过 `UnifiedExpressionEngine`（Helper 层，封装表达式解析）在 `Init` 时预编译 `SkillCondition`；索敌时 `FilterTargets` 过滤目标；`OrderExpression` 作为 `GetSortOrder3` 的数值键（`Evaluate<float>`）。

#### (7) 连发（Burst）

`SkillData.BurstCount > 0` 时：`Cast` 记录 `LastTargets` → `Burst()` 每 `BurstDelay` 对 `LastTargets` 结算一次（`BurstFind/RegetTarget` 时重索敌）→ 计数归零结束。

#### (8) 技能升级 `DoUpgrade(int skillId)`

`StartId` 记录升级前 id → 换 `Id` → 重载 `Modifies`、攻击点、技力 → `Reset()`。干员离场时还原（`DoUpgrade(StartId)`）。

#### (9) 攻击范围显示 `ShowUnitAttackArea / HideUnitAttackArea`

按 `AttackPoints` 或 `AttackRange` 实例化 `ShowRange` 地块高亮（数据 `Data.Color/Alpha` 控制样式）。

### 5.2 `SkillData` 配置字段速查（`Config/SkillData.cs`，115 字段）

按用途分组：

| 分组 | 字段 |
|---|---|
| 基础 | `Id / Type（反射类名）/ Name / Desc / Icon / Upgrade` |
| 触发 | `Trigger`（TriggerEnum 事件触发）、`SkillCondition`（表达式）、`IgnoreStun` |
| 使用类型 | `ReadyType / UseType / SkillCost / MaxUseCount / AutoUse / NoTargetAlsoUse / RegetTarget / StopBreak / EffectiveRate / StopOtherSkill` |
| Buff 开关 | `EnableBuff / DisableBuff / TargetEnableBuff / TargetDisableBuff / OpenDisable` |
| 索敌 | `TargetTeam（位掩码）/ TargetFilter / DeadFind / AttackFly / UseEventUser / UseEventTarget / AntiHide / IgnoreSleep / IgnoreSelectable / MidLimit / UnitLimit / ProfessionLimit / RareLimit / CostLimit / PosLimit / SelfHpLess / TargetHpLess / TargetHpMore / AttackRange / AttackPoints / AttackPoint（命中点时随机补足 HitCount）/ AttackAreaWithMain / ExAttackPoints（运行时）` |
| 排序 | `AttackOrder / AttackOrder2 / OrderTag / OrderBuff / OrderExpression` |
| 伤害 | `DamageType / IfHeal / DamageRate / DamageWithFrameRate / DamageBase（0自身攻/1目标MaxHp/2固定/3主子攻）/ LifeSteal / DamageCount / MinDamageRate（经Unit）` |
| 范围伤害 | `AreaRange / AreaPoints / AreaNoCheck / AreaMainDamage / AreaDamage` |
| 连发 | `BurstCount / BurstDelay / BurstFind` |
| 技能树 | `Skills（附带技能）/ ExSkills / ExSkillWeight（附加技能）/ UpgradeSkill（开启结束升级）` |
| 时序 | `Cooldown / OpenTime / CanStop / StartPower / MaxPower / PowerCount / PowerType / PowerStopNeed / PowerUseType` |
| 动画 | `ModelAnimation / ModelAnimationDown / AnimationTime / OverwriteAnimation / OverwriteAnimationDown / AttackMode（跟随攻击/固定间隔）/ DisableScaleX` |
| 弹道 | `Bullet / ShootPoint` |
| 修饰 | `Modifys / ModifyDatas / ElementInjure` |
| 状态施加 | `Buffs / BuffData / BuffData2 / BuffData3 / BuffLastTime / BuffChance / BuffRemoves / BuffRely` |
| 特效 | `ReadyEffect / StartEffect / CastEffect / HitEffect / EffectEffect / GatherEffect / LoopStartEffect / LoopCastEffect` |
| 其他 | `Data（扩展字典：ShowRange/ShowBar/Color/Alpha/HitCount/ElementType/UnitId...）/ NotAttackFlag / CanDestory` |

### 5.3 技能类型总表（`Skill/Types/`）

> 55 个技能子类按功能可分为：**通用/空壳**（普通技能、全局技能）、**目标/事件特化**（事件目标替换、余火墙、乌萨斯战吼、塑灵术士式索敌、近战/群攻）、**伤害/状态施加特化**（获得费用、回复技力、技能升级、赋予被动、增加修饰器）、**位移/地形**（传送、推、拉、跳跃、拆地板、地块属性更改、重寻路、尝试插入临时路径点）、**召唤部署**（部署干员系 5 个 + 获取单位 + 召唤绑定 + 引爆召唤 + 召唤）、**全局规则**（修改Tag/回费速度/部署上限、关卡伤害、暂停、清除射弹）、**生命周期**（强制撤退、打断释放、持续施法、锁血治疗）等。
>
> 完整逐个说明见 **附录 A**（含继承族谱与实现缺陷清单）。

---

## 6. Buff 状态系统

### 6.1 `Buff` 基类（`Buff/Buff.cs`，228 行）

Buff 是"作用在单位/弹道上的一段声明式效果"：挂载时生效、持续期间逐帧维持、结束/被清除时移除。

#### (1) 核心字段

| 字段 | 说明 |
|---|---|
| `Id` / `BuffData` | 配置索引 / 配置（`Type` 反射类名） |
| `Unit` / `Bullet` / `Skill` | 作用对象（单位或弹道二选一）/ 来源技能 |
| `SourceUnit => Skill.Unit` | 施加者（"来源"） |
| `Index` | 施加序号（对应 `SkillData.BuffData[i]` 参数下标） |
| `LastTime` | 覆盖持续时间（`<0` 时用配置 `BuffData.LastTime`，可被 `SkillData.BuffLastTime` 覆盖） |
| `Duration`（CountDown） | 持续时间倒计时 |
| `LastingEffect` | 持续特效（`BuffData.LastingEffect`） |
| `RelayBuff` | 依赖 Buff 实例（`BuffData.RelyBuff`，依赖消失则自身结束） |
| `Dead` | 已结束标记 |
| `isBlocking` | 被"Buff抵挡"延迟生效的剩余时间 |
| `CancelsCancelableBuffs / MakesBuffsCancelable` | 入梦砖（Buff抵挡/可抵挡）机制 |

#### (2) 生命周期

- **`Init()`**：算持续时间 → 生成持续特效 → 若 `RoundNeed == 1` 计算自身周围一圈（十字）格 → **抵挡检查**（目标有 `Buff抵挡` 且来源有 `Buff可抵挡` 且非 `NotCancelable` 时：时长超过抵挡剩余则延迟入列，否则直接 `Finish` 被挡掉）。
- **`Enable()`**：生效前置条件——阻挡数要求（`StopNeed/StopLess`）、周围格要求（`RoundNeed`，范围内超过 1 个单位则不生效）。
- **`ApplyToUnit()` / `ApplyToBullet()`**：把效果"刷"到单位/弹道上（**每帧 `Refresh()` 时对所有 Buff 重放**，所以加值类 Buff 写成"累加修正字段"，减益写成"覆盖状态布尔"）。
- **`Reset()`**：重复施加时调用——重算时长、抵挡重检、`Upgrade`（升级成更强 Buff）、`IfSwitch`（切换类直接结束旧的）。
- **`Update()`**：抵挡转移 → 依赖技能存活检查（`BuffRely`）→ 依赖 Buff 检查 → 抗性加速流逝（`Resist`：`Duration.Update(dt / Unit.Resist)`）→ 到时 `Finish()`。
- **`UpdateView()`**：表现刷新钩子。
- **`Finish()`**：`Dead = true` → `Unit.RemoveBuff`（并从 `DamageRewrites` 摘除）→ 归还特效。

### 6.2 `Unit.AddBuff`（反射工厂，`Unit/Unit.cs`）

```csharp
AddBuff(int buffId, Skill source, int index, float lastTime = -1)
 ├─ IgnoreBuffs 免疫检查
 ├─ RelyBuff 依赖检查
 ├─ 重复检查：同 Id 或已存在升级版（UnSourceCheck 决定是否限来源）→ 旧 Buff.Reset()
 ├─ 反射创建 Buffs.<Type> → 挂 Id/Skill/Unit/LastTime/Index
 ├─ 若实现 IDamageRewrite → 注册进 DamageRewrites（按 OrderCode 排序）
 └─ newBuff.Init() → Unit.Refresh() 立即重算属性
```

- `Bullet.AddBuff` 结构相同（挂到 `Bullet.Buffs`，`ApplyToBullet` 每帧刷弹道属性）。

### 6.3 伤害重写管线（`IDamageRewrite`）

```csharp
public interface IDamageRewrite { void DamageRewrite(DamageInfo dmg); int OrderCode { get; } }
```

- 单位受伤（`Unit.Damage`）在闪避判定后、扣血前，按 `OrderCode` 升序依次执行 `DamageRewrites`：
  - `护盾`（每次挡 1 次，扣 `Count`）、`屏障`（按数值吸收）、`锁血`（把伤害钳到剩余 HP-1 之类）、`减伤/吸收类限伤/伤害分摊/无敌/闪避/无视防御/未阻挡伤害` 等（`Buff/Types/伤害重写类/`，详见附录 B）。

### 6.4 数值变化系统（`Buff/Types/数值变化/`）

- `数值变化`（基类）：从 `BuffData.Data["t"]` 读**要修改的 Unit/Bullet 字段名数组**，`ApplyToUnit/ApplyToBullet` 用反射把 `Skill.SkillData.GetBuffData(Index)[i]` 加到对应字段上（`field.SetValue(unit, baseValue + value)`）。
  - 子类：`数值变化叠加`（按层数叠加）、`数值变化衰减`（随时间衰减）、`数值变化取高`（取更高值）、`数值变化自来源`（以来源单位属性为基数）、`数值变化依表达式`（表达式计算数值）。
- 这是新版属性修改的通用通道，配合 `SkillData.BuffData/BuffData2/BuffData3` 参数数组使用。

### 6.5 状态类 Buff 的写法约定

由于 `Unit.UpdateBuffs()` 每帧先复位再重放：

```csharp
// 基类每帧复位
IfHide = hideBase; IfHideAnti = false; IfSleep = false;
IfSelectable = true; CanStopOther = true; IfStun = false;
// 各 Buff 在 ApplyToUnit / Update 中重新置位（如 眩晕 → IfStun = true）
```

因此"眩晕/睡眠/隐身/缴械/不可阻挡"等 Buff 只需在 `ApplyToUnit()`（或每帧）把对应布尔置位即可，无需自己管理清除。

### 6.6 推拉 Buff（`IPushBuff`）

- `IPushBuff { Vector2 GetPushPower(); void Update(); }`（`Buff/Types/IPushBuff.cs`）。
- `推动/拉动` 类 Buff 被加入 `Unit.PushBuffs`，`UpdatePush()` 合力位移；失衡期间打断施法。具体见附录 B。

### 6.7 `BuffData` 配置字段速查

| 字段 | 说明 |
|---|---|
| `Id / Type / Name` | 标识与反射类名 |
| `LastTime` | 持续时间 |
| `Upgrade` | 到期/重复时升级成的 Buff id |
| `RelyBuff` | 依赖的 Buff id（缺失则不挂载/到时结束） |
| `Resist` | 是否受"抵抗"属性加速（`Resist>0` 时时间流逝加快） |
| `StopNeed / StopLess` | 需要阻挡数 / 阻挡数少于某值才生效 |
| `RoundNeed` | 需要周围一格无其他单位才生效 |
| `UnSourceCheck` | 重复检查是否忽略来源 |
| `IfSwitch` | 重复施加时先结束旧实例 |
| `LastingEffect` | 持续特效 id |
| `DeadRemain` | 单位死亡后是否保留 |
| `NotCancelable` | 是否不可被"Buff抵挡"抵消 |
| `Data` | 扩展字典（各子类自定义参数） |

### 6.8 Buff 类型总表（`Buff/Types/`）

> 54 个 Buff 子类按功能可分为：**数值修改**（数值变化族 6 个、设置属性、高级属性设置、免疫Buff、绝食、缴械、不可阻挡）、**状态控制**（眩晕、睡眠、冻结、麻痹、打断攻击、专注失调、即死、破坏、反隐、隐身）、**持续伤害/治疗**（中毒、剧毒、线性剧毒、治愈、消去伤害、延迟追加Buff、蕾缪安通缉类停留时间标记）、**位移**（推动、拉动、重设高度）、**伤害重写**（伤害重写/吸收类限伤、屏障、护盾、锁血、无敌、伤害分摊）、**伤害面板修改**（减伤、未阻挡伤害、闪避、无视防御、Buff可抵挡）、**特殊机制**（Buff抵挡、叠层转化、加速、覆盖动作、屏蔽模型、精确移除Buff、m3融毁加攻）等。
>
> 完整逐个说明见 **附录 B**（含汇总表、关键机制注解与问题清单）。

---

## 7. 子弹 / 修饰器 / 地图 / 表现 子系统

### 7.1 弹道 `Bullet`（`Bullet/Bullet.cs`）

- 非 MonoBehaviour 逻辑对象，由 `Battle.CreateBullet` 创建、`Battle.Bullets` 管理、`BulletManager` 管理模型对象池。
- 字段：`Skill`（来源技能）、`Target / TargetPos`、`Position / Direction / Speed`、`Attack`（伤害倍率，随 Buff 变化）、`Modifies`（弹道修饰器，按 OrderCode 排序）、`Buffs`（弹道上的 Buff）。
- 生命周期：`Init`（读 `BulletData.Modifys` 创建修饰器、创建模型）→ `Update`（每帧 `UpdateBulletAttr`：刷 Buff → 算速度/攻击）→ 命中时由子类调用 `Skill.Hit(target, bullet)` 结算（`IBulletDamageModify` 修饰器在此注入）→ `Finish`（归还模型）。
- 子类（`Bullet/Types/`）：`子弹`（直线追踪）、`冲击波`（范围扩散）、`大飞镖`（回转）、`钩子`（拉人）、`链式弹道`（弹跳衰减，`reductionRate`）、`延迟打击`、`持续施法子弹`、`棘刺2` 等（详见附录 C）。

### 7.2 修饰器 `Modify`（`Modify/IModify.cs` + `ModifyManager.cs`）

- `Modify` 基类：`Id / ModifyData / Skill / orderCode`，`Init()` 从 `Data["OrderCode"]` 读排序码。
- 工厂 `ModifyManager.Get(id, skill/bullet)` 反射创建 `Modifys.<Type>`。
- 接口族（决定修饰器在哪一步介入）：

| 接口 | 时机 | 例子 |
|---|---|---|
| `IDamageModify` | `GetDamageInfo` 时修改伤害面板 | `暴击`（随机暴击倍率）、`对低血伤害`（目标血低增伤）、`对飞行伤害`、`对远程伤害`、`对buff伤害`、`最大生命伤害`、`额外伤害加`、`穿甲`（DefIgnore） |
| `ISelfDamageModify` | 自身 Buff 对自身输出的修改 | `蓄力增伤` 等 |
| `ITargetModify` | 索敌目标数（`GetTargetCount`） | `额外目标`、`额外目标与伤害`、`额外目标依层数`、`对个数伤害` |
| `IUnitModify` | `addSkillEffect` 时对目标直接改属性 | `对buff回复技力`、`链奶伤害衰减` 等 |
| `IBulletDamageModify` | 弹道命中时改伤害 | 弹道挂载的修饰器 |
| `ISkillModify` | 修改技能本身 | （预留） |

### 7.3 地图 `Map` / `Tile`（`Map/`）

- `Map`：持有 `Tile[,]`，由场景 `MapGrid` 组件初始化；记录边界（minX/minZ/maxX/maxZ）。
- `Tile : ITileData`：地块属性 `CanBuildUnit / FarAttackGrid（远程高台）/ Passable / PassCost / Tag`；`Units / MidUnits` 站场单位列表；`CanSet(UnitData)` 部署合法性（高台/地面、仅无单位/仅无敌人等标签、`仅在单位攻击范围内:xxx` 特殊部署位）。
- `PathFinder_AStar`：A* 寻路（配合 AstarPathfinding 插件）。
- `MapUnitInfo`：地图预置单位的入场信息（时间/标签/位置/方向/寿命）。

### 7.4 表现层 `View/`（简述）

| 类 | 职责 |
|---|---|
| `UnitModel` | 单位模型根（Spine 动画播放、骨骼点 `GetPoint`、`ShowPower/ShowHeal/ShowCrit/SetColor` 等战斗反馈） |
| `PlayerUnitModel` | 干员模型（前后朝向 `Forward`、方向箭头） |
| `BulletModel` | 弹道模型 |
| `MapTile` | 地块表现 |
| `PitModel / PullLineModel` | 坑/拉线表现 |
| `TrailManager` | 路径轨迹线（编辑器） |
| `BattleCamera` | 战斗相机 |
| `SpineModel / NormalModel` | 骨骼/普通模型组件 |
| `BattleManager` | 战斗驱动门面（§3.6） |

### 7.5 特效 `Effect`（`Effect/`）

- `Effect : MonoBehaviour` + `EffectManager`（对象池）；`PullEffect`（拉人特效，已废弃死代码）；`旧Effect`（旧版副本，已废弃）。

> 各子系统子类完整清单见 **附录 C**。

---

## 8. 扩展指南：如何新增技能 / Buff / 单位

### 8.1 新增一个技能类型

1. 在 `Assets/Scripts/Core/Skill/Types/` 新建 `class 我的技能 : Skill`（命名空间 `Skills`），重写需要的虚方法：
   - 索敌定制 → `GetAttackTarget / FindTarget / SortTarget / FilterTarget`；
   - 生效定制 → `Effect / Hit / Cast / Start`；
   - 周期行为 → `Update / UpdateOpening`。
2. 在 Excel 技能表新增一行：`Type = 我的技能`，填好 `SkillData` 各字段；导表生成 `Data/SkillData.txt`。
3. 单位表 `Skills` 列引用该技能 id 即可生效。

> 常用技巧：纯"加状态"技能不重写任何方法，只配 `Buffs + BuffData` 数组；纯数值变化用 `Buff 数值变化` + `t` 字段名。

### 8.2 新增一个 Buff 类型

1. 在 `Assets/Scripts/Core/Buff/Types/` 新建 `class 我的状态 : Buff`（命名空间 `Buffs`）：
   - 改属性 → `ApplyToUnit()` 中累加 `Unit.AttackAdd / AttackRate ...` 等修正字段（框架每帧 `Refresh` 重算）；
   - 改状态 → `ApplyToUnit()` 中置位 `Unit.IfStun / IfSleep / IfHide ...`；
   - 改伤害 → 实现 `IDamageRewrite`（进 `Unit.DamageRewrites`）或 `IDamageModify / ISelfDamageModify`（进 `GetDamageInfo`）；
   - 位移 → 实现 `IPushBuff`。
2. 参数从 `SkillData.BuffData/2/3`（按 `Index`）或 `BuffData.Data` 字典读取。
3. Excel buff 表新增行：`Type = 我的状态`。

### 8.3 新增一个单位类型

1. 在 `Assets/Scripts/Core/Unit/Types/` 新建 `class 我的单位 : Unit`（命名空间 `Units`），重写 `Init / UpdateAction / Finish / IfStoped / distanceToFinal ...`。
2. Excel 单位表新增行：`Type = 我的单位` + 属性/模型/技能配置。
3. 若用于敌人：在波次表引用；若用于干员：编队表引用（含 `CanSetPos` 部署标签）。

---

## 9. 已知问题与遗留代码

> 以下均摘自代码中的注释与实现观察，供后续维护参考。

1. **索敌效率与复杂度**（README 原文）：索敌逻辑较乱，`FindAll(pos, radius, team)` 直接遍历全单位列表（`//需要优化！`）；`Skill.SortTarget` 排序在每帧 `GetAttackTarget` 里重算。
2. **持续施法类技能全靠 trick**（README 原文）：`持续施法.cs` 通过 `LastTarget` 缓存目标 + 目标失效即 `Opening.Finish()` 的方式模拟持续施法。
3. **反射 + 字符串拼接脆弱性**：所有 `Type` 字符串与命名空间硬绑定，配置写错即创建失败（有 Tip 提示）；`Skill.LearnSkill` 用 `nameof(Skills) + "." + type`，若 type 含命名空间会失败。
4. **遗留/半成品系统**：
   - `ISkillDiy.cs`：`ITargetSelector / IFilterStrategy / ISortStrategy / IExecutor` 等"动态索敌"接口大量注释、半实现；`DynamicTargetSelector / DynamicSorter / TargetSelectorFactory / SortStrategyFactory` 为实验性代码，实际索敌主线并未使用（仍走 `Skill.GetAttackTarget` 虚方法管线）；`主要目标选择器.cs` 中大部分选择器被注释标记"弃用的选择器"，仅 `Buff筛选` 等少数类存活。
   - `Unit.cs` 中"入梦砖"相关 `UpdateBuffSuppression` 大段注释。
   - `旧Effect.cs` 已废弃；`Map.cs` 内旧广搜寻路已注释（改用 A* 插件）。
5. **硬编码**：`Skill.cs` 中 `SkillData.Id == "萃蔓无敌"` 的调试日志；`Battle.cs` 注释掉的 Dungeon 分支；`Unit.Damage` 中 `DamageTypeEnum.Real/Element` 统计逻辑分支；`Battle.CreateEnemy` 中找不到单位时兜底 `enemy_1106_byokai`。
6. **编码问题**：部分源文件为 GBK 编码（已确认：`ISkillDiy.cs`、`DynamicTargetSelector.cs`、`DynamicSorter.cs`、`TargetSelector/主要目标选择器.cs`、`TargetSelector/主要目标排序器.cs`、`Preview.cs`、`Map/ITileData.cs`、`Map/PathFinder_AStar.cs`；注意 Skill/Types、Buff/Types、Bullet、Modify 目录经严格 UTF-8 校验全部合法），GBK 与 UTF-8 文件混存，迁移仓库时需统一。
7. **动画/攻速耦合**：`Start()` 中动画时长与攻击间隔互相钳制（`fullDuration * attackSpeed != Cooldown.value` 时强制拉快/拉长动画），逻辑较绕，改动需谨慎。
8. **Buff 抵挡（入梦砖）机制**：`Buff抵挡` + `Buff可抵挡` 双条件 + `NotCancelable` 豁免，逻辑分布在 `Buff.Init/Reset/Update` 与 `Unit.AddBuff`，是项目中最特殊的状态交互。

---

## 附录 A：Skill 子类全表（`Skill/Types/`，55 个文件）

> 全部类均在 **`Skills` 命名空间**（基类 `Skill` 在全局命名空间）。含 `召唤相关/` 子目录 7 个。配置 `Type` 字符串 = 类名。**本目录全部为合法 UTF-8**（与 Buff 目录相同，无 GBK 文件）。

### A.1 汇总表

| 类名 | 文件 | 作用一句话 | 关键重写 |
|---|---|---|---|
| `普通技能` | 普通技能.cs | 空壳，直接用基类默认行为 | 无 |
| `全局技能` | 全局技能.cs | 空壳，"全局"语义标记类型（进 `Database.globalSkills`） | 无 |
| `乌萨斯战吼` | 乌萨斯战吼.cs | 事件中先锋职业者成为目标并触发加费 | FindTarget |
| `事件目标替换` | 事件目标替换.cs | 用事件栈 Target 替换/追加索敌目标 | FindTarget |
| `传送` | 传送.cs | 按模式把敌人传送到指定点（自身/目标/攻击点，强制/可达/距离模式） | Init / Start / Cast |
| `伤害连发` | 伤害连发.cs | 空类（原连发逻辑全部注释，**已废弃**） | 无 |
| `余火墙` | 余火墙.cs | 事件双方在自身朝向直线两侧且伤害类型匹配时攻击事件 User | Init / FindTarget |
| `修改Tag` | 修改Tag.cs | 命中时修改波次 Tag（配合 Tag 波次触发） | Effect |
| `修改回费速度` | 修改回费速度.cs | 命中时按系数累乘修改全局回费速度 | Effect |
| `修改部署上限` | 修改部署上限.cs | 命中时修改全局可部署数上限 | Effect |
| `关卡伤害` | 关卡伤害.cs | 命中时对关卡生命值造成固定伤害 | Effect |
| `回复技力` | 回复技力.cs | 命中时给目标所有技能随机回技力 | Hit |
| `回复技力1` | 回复技力1.cs | 命中时按目标技能最大技力百分比回技力 | Hit |
| `地块属性更改` | 地块属性更改.cs | 改目标格可通行性/通行代价并触发全场重寻路 | Init / Cast |
| `塑灵术士式索敌` | 塑灵术士式索敌.cs | 索敌额外包含被召唤物（token）阻挡的单位 | GetAttackTarget |
| `增加修饰器` | 增加修饰器.cs | 给目标指定技能动态挂修饰器 | Effect |
| `子弹属性修改` | 子弹属性修改.cs | 收集范围内子弹（**无后续修改逻辑，半成品**） | Cast |
| `尝试插入临时路径点` | 尝试插入临时路径点.cs | 给敌人插入限时临时路径点改道 | Init / Update / Start / Cast |
| `引爆召唤` | 引爆召唤.cs | 合并召唤物攻击范围索敌，命中后引爆召唤物 | FindTarget / Effect |
| `弩箭` | 弩箭.cs | 沿直线/地图边缘发射线列多颗平行子弹 | Init / Effect |
| `强制撤退` | 强制撤退.cs | 倒计时后强制自身撤退（死亡）并返还费用 | Effect / Update / Finish |
| `打断释放` | 打断释放.cs | 被强制打断时立即结算一次技能（抢先释放） | BreakCast |
| `技能升级` | 技能升级.cs | 把目标技能升级为配置技能（缺失则施法者学会） | Init / Effect |
| `拆地板` | 拆地板.cs | 随机拆除自身周围地块并击杀格上单位 | Ready / Effect |
| `拉` | 拉.cs | 按"推力-重量"给目标施加拉动 Buff（IPushBuff） | addBuff |
| `持续施法` | 持续施法.cs | 锁定目标持续施法，目标失效/眩晕提前结束 | Update / UpdateOpening / FindTarget |
| `持续获得费用` | 持续获得费用.cs | 开启期间按命中逐步累积费用，结束补发 | Hit / OnOpenEnd |
| `掉落` | 掉落.cs | 击杀格内目标并使其下坠（空中单位坠落） | Init / Hit |
| `推` | 推.cs | 按"推力-重量/角度"给目标施加推动 Buff | addBuff |
| `显示进度` | 显示进度.cs | 空实现，仅暴露干员/位置/方向字段 | Start（仅 base） |
| `暂停` | 暂停.cs | 生效时暂停全场时间，开启结束恢复（时停） | Cast / OnOpenEnd |
| `死亡回技力` | 死亡回技力.cs | 事件目标被命中时给主技能回技力 | Init / Cast |
| `清除射弹` | 清除射弹.cs | 清除范围内所有敌方子弹 | Cast |
| `溢出打数转化` | 溢出打数转化.cs | "打数溢出"计数转为额外目标数并加入 ExTarget | orderTargets / GetTargetCount |
| `生命值检测技能`⚠️ | 生命值检测.cs | 目标血量 ≤ 攻击×系数才可选为目标（**类名≠文件名**） | Init / CanUseTo（隐藏非重写） |
| `继承主技能` | 继承主技能.cs | 复制来源单位 MainSkill 为自身主技能 | Init / Start / Cast |
| `继承属性` | 继承属性.cs | 反射复制来源单位字段到自身 | Init / Start / Cast |
| `群攻` | 群攻.cs | 近战群攻，目标数 = 自身阻挡数 | GetTargetCount |
| `获取子弹` | 获取子弹.cs | 收集范围内子弹（只收集） | Cast |
| `获取额外攻击范围` | 获取额外攻击范围.cs | 目标格作为额外攻击点附加到指定技能 | Init / Start / OnOpenEnd |
| `获得费用` | 获得费用.cs | 施法时一次性获得费用 | Cast |
| `赋予被动` | 赋予被动.cs | 给所有目标授予配置的被动技能 | Init / Start |
| `跳跃` | 跳跃.cs | 敌人被阻挡时可跳跃越过 | Init / Ready / Cast |
| `近战` | 近战.cs | 优先攻击阻挡目标并按阻挡顺序排序 | FindTarget / SortTarget |
| `重寻路` | 重寻路.cs | 施放时通知全场敌人重寻路 | Start |
| `锁血治疗` | 锁血治疗.cs | 锁血到阈值后持续回血 | Update / Cast |
| `非指向技能` | 非指向技能.cs | 无目标也能释放的简化流程基类（Effect(null) 直接生效） | Update / Start / Cast / Burst |
| `召唤` | 召唤相关/召唤.cs | 敌人在周围随机位置生成 Count 个同波次新怪 | Init / Effect |
| `召唤绑定` | 召唤相关/召唤绑定.cs | 召唤物死亡即结束技能开启状态 | Update |
| `获取单位` | 召唤相关/获取单位.cs | 生成受场上数量上限约束的召唤物子单位 | Init / Useable / Cast / DoOpen |
| `部署干员` | 召唤相关/部署干员.cs | 核心召唤部署：目标位置部署干员并绑定父子关系 | Init / Start / GetPosList / GetToken / SetToken |
| `部署装置` | 召唤相关/部署装置.cs | 部署带持续时间/朝向的中立装置 | Init / Start / SetToken |
| `位移` | 召唤相关/位移.cs | 把施法干员自身移动到目标格 | Start / GetToken / SetToken |
| `ew3类部署干员` | 召唤相关/ew3类部署干员.cs | "圆+攻击点"区域内按距离批量部署同 ID 干员 | Init / Start / GetPos / GetTile / SetToken |
| `ew3类部署装置` | 召唤相关/ew3类部署装置.cs | "圆+攻击点"区域内批量部署中立装置 | Init / Start / SetToken |

### A.2 技能类族谱（继承关系）

```
Skill（基类，全局命名空间）
├─ 普通技能 / 全局技能（空壳）
├─ 非指向技能（无目标可释放）
│   ├─ 修改Tag / 修改回费速度 / 拆地板 / 弩箭
├─ 近战（优先阻挡目标）
│   └─ 群攻（目标数=阻挡数）
├─ 获得费用
│   └─ 乌萨斯战吼
├─ 部署干员（核心召唤部署）
│   ├─ 部署装置（中立装置）
│   ├─ 位移（移动自身）
│   └─ ew3类部署干员（圆+攻击点批量）
│       └─ ew3类部署装置
└─ 其余均为直接 : Skill 的独立类型
```

### A.3 值得注意的实现缺陷/风险点

1. **`生命值检测技能` 的 `CanUseTo` 是隐藏（`new`）而非重写**：基类 `Skill.CanUseTo` 是**非虚方法**（Skill.cs:277），基类索敌链（`orderTargets` 的 `RemoveAll(x => !CanUseTo(x))`）调用的是基类版本，子类血量条件实际不生效。
2. **`持续施法` Update 列表错位**：检查 `LastTargets[i]`（基类字段）却移除 `LastTarget.RemoveAt(i)`（自身字段），两列表不同步。
3. **事件上下文强依赖**：`余火墙` / `死亡回技力` 直接 `TriggerDatas.Peek()` 或解引用事件目标，脱离事件上下文会空引用/抛异常。
4. **半成品**：`子弹属性修改` / `获取子弹` 只收集子弹不消费；`伤害连发` / `显示进度` 为空实现。
5. **`继承主技能`**：`CopyState` 不复制 `Unit` 字段，靠手动赋值；`Cast/Start` 未调 base，无伤害/CD 流程。
6. **`弩箭`** MapUp 分支用 `GetLength(1)` 作 x 上限（疑似行列写反）。
7. **部署系**：`battleOp.Count > 0 && battleOp.Count < i` 条件基本不会成立；`Operator` 字段在子类中反复用 `new` 隐藏。

## 附录 B：Buff 子类全表（`Buff/Types/`，54 个文件）

> 全部类（含子目录 `伤害重写类/`、`数值变化/`、`Sp/`）均在 **`Buffs` 命名空间**（`IPushBuff` 是全局命名空间的接口）。配置 `Type` 字符串 = 类名。

### B.1 汇总表

| 类名 | 文件 | 作用一句话 | 接口/关键重写 | 影响 |
|---|---|---|---|---|
| `IPushBuff` | IPushBuff.cs | 推拉接口 | 接口 | 驱动位移 |
| `中毒` | 中毒.cs | 周期性毒伤 DoT（3 种基准） | Init/Update | 对自身 Damage（Real/配置类型） |
| `免疫Buff` | 免疫Buff.cs | 免疫指定 buff 列表 | Init/Finish | `Unit.IgnoreBuffs` 增删 |
| `冻结` | 冻结.cs | 冻结（眩晕+锁动画） | Init/Update/Finish | IfStun、AnimationSpeed、CanChangeAnimation |
| `反隐` | 反隐.cs | 反隐 | Update | IfHideAnti |
| `打断攻击` | 打断攻击.cs | 打断施法/攻击 | Init | `BreakAllCast()` |
| `拉动` | 拉动.cs | 拉力拉向施法者（衰减） | IPushBuff; Init/Update/Finish | 位移；StopUnit 联动 |
| `推动` | 推动.cs | 推力推离（衰减） | IPushBuff; Update/Finish | 位移；**不调 base.Update** |
| `普通Buff` | 普通Buff.cs | 空占位 buff | 无 | 无 |
| `消去伤害` | 消去伤害.cs | 结束时按 MaxHp 比例真伤 | Finish | 自身 Real Damage |
| `眩晕` | 眩晕.cs | 眩晕（可选动画） | Init/Update | IfStun、CanStopOther、SetStatus(Stun) |
| `睡眠` | 睡眠.cs | 睡眠 | Update | IfSleep、IfStun、Stun 动画 |
| `破坏` | 破坏.cs | 禁用可破坏技能 | Init/Finish | skill.Destroyed |
| `线性剧毒` | 线性剧毒.cs | 线性递增毒伤 | Init/Update | 自身 Real Damage（线性插值） |
| `覆盖动作` | 覆盖动作.cs | 覆盖待机/死亡/移动动画 | Init/Finish | OverWriteIdle/Die/Move/Animation |
| `麻痹` | 麻痹.cs | 禁用技能施放 N 次 | Init/Update/Finish | skill.IsCantCastCount |
| `精确移除Buff` | 精确移除Buff.cs | 持续移除指定 buff | Init/Update | 目标 buff.Finish() |
| `隐身` | 隐身.cs | 隐身（停下后延迟隐身） | Init/Update/UpdateView | IfHide；LastingEffect 显隐 |
| `治愈` | 治愈.cs | 周期性治疗（低血强化） | Init/Update | Unit.Heal；参考 MaxHp/Attack |
| `延迟追加Buff` | 延迟追加Buff.cs | 延迟后追加 buff 列表 | Init/Update | 追加 Unit.AddBuff |
| `Buff可抵挡` | Buff可抵挡.cs | 可抵挡标记；目标有 Buff抵挡时伤害归零 | ISelfDamageModify+IDamageModify | Attack=0、Avoid=true |
| `Buff抵挡` | Buff抵挡.cs | 抵挡/延迟 buff，结束时重放 | Init/Finish | 与 Buff.isBlocking 联动 |
| `蕾缪安通缉类停留时间标记` | 蕾缪安通缉类停留时间标记.cs | 攻击范围停留达标→加通缉 buff | Init/Update/Finish | 敌人 AddBuff；仅干员 |
| `剧毒` | 剧毒.cs | 加速毒伤（LoseHP） | Init/Update | 自身 LoseHP Damage |
| `叠层转化` | 叠层转化.cs | 叠满层转化为另一 buff | Init/Reset | Level++；AddBuff(BuffId) |
| `加速` | 加速.cs | 叠加速移速，攻击消耗层数附加伤害 | ISelfDamageModify; Init/Update/ApplyToUnit | SpeedRate；DamageInfo.Attack += Speed×damageRate×2 |
| `重设高度` | 重设高度.cs | 起飞→维持→降落高度变化 | Init/ApplyToUnit/Update/Finish | Unit.Height |
| `设置属性` | 设置属性.cs | 反射绝对赋值属性，结束还原 | Init/ApplyToUnit/Finish | 任意 float 字段 |
| `屏蔽模型` | 屏蔽模型.cs | 隐藏模型/影子 | Init/ApplyToUnit/Finish | UnitModel.hide/show |
| `绝食` | 绝食.cs | 不可治疗（可限定来源） | ApplyToUnit/Finish | CanBeHeal、HealOnly |
| `不可阻挡` | 不可阻挡.cs | 敌人不可阻挡 | ApplyToUnit | 敌人.UnStopped |
| `缴械` | 缴械.cs | 禁止攻击 | ApplyToUnit | CanAttack |
| `专注失调` | 专注失调.cs | 强制自动开主技能 | Update | MainSkill.DoOpen() |
| `即死` | 即死.cs | 立即处决（先触发致命事件） | Init/Update | Trigger(致命)+DoDie |
| `高级属性设置` | 高级属性设置.cs | 同 `设置属性`（重复类） | Init/ApplyToUnit/Finish | 任意 float 字段 |
| `伤害重写` | 伤害重写类/伤害重写.cs | 限伤封顶 | IDamageRewrite | FinalDamage 封顶 MinResponseLimit |
| `吸收类限伤` | 伤害重写类/吸收类限伤.cs | 与 `伤害重写` 逐字节相同 | IDamageRewrite | 同上 |
| `减伤` | 伤害重写类/减伤.cs | 按比例减伤（可限定来源） | IDamageModify | DamageRate ×= rate |
| `屏障` | 伤害重写类/屏障.cs | 按类型吸收伤害（可衰减） | IDamageRewrite; Update | FinalDamage 被 Count 抵扣 |
| `护盾` | 伤害重写类/护盾.cs | 次数护盾 | IDamageRewrite | 正伤害清零、Count-- |
| `无视防御` | 伤害重写类/无视防御.cs | 概率无视防御（开技双概率） | ISelfDamageModify | DefIgnore、DefIgnoreRate |
| `未阻挡伤害` | 伤害重写类/未阻挡伤害.cs | 对未阻挡目标增减伤 | IDamageModify | DamageRate ×= rate |
| `锁血` | 伤害重写类/锁血.cs | 锁血阈值 | IDamageRewrite; Init/Update/Finish | Unit.Hp 钳制；FinalDamage 归零 |
| `闪避` | 伤害重写类/闪避.cs | 按类型概率闪避 | IDamageModify | Avoid=true |
| `无敌` | 伤害重写类/无敌.cs | 无敌+不可选中 | IDamageRewrite; Update | FinalDamage=0；IfSelectable=false |
| `伤害分摊` | 伤害重写类/伤害分摊.cs | 同组按比例分摊伤害 | IDamageRewrite; Init | 主/无主双模式分摊 |
| `数值变化` | 数值变化/数值变化.cs | 反射加法数值 buff（单位/子弹通用） | Init/ApplyToUnit/ApplyToBullet | 任意 float 字段 += 数值 |
| `数值变化依表达式` | 数值变化/数值变化依表达式.cs | 表达式赋值改数值（可限次数） | Init/ApplyToUnit/ApplyToBullet/Reset | 表达式引擎写字段 |
| `数值变化取高` | 数值变化/数值变化取高.cs | 取当前值与目标值较高 | ApplyToUnit/ApplyToBullet | 字段 = max(当前,目标) |
| `数值变化叠加` | 数值变化/数值变化叠加.cs | 层数×数值的可叠加变化 | Init/Reset/Update/Finish/GetValue | 字段 += 数值×Level；降层续时 |
| `鼓舞`⚠️ | 数值变化/数值变化自来源.cs | 数值×施法者攻击力（**类名≠文件名**） | Init/ApplyToUnit/ApplyToBullet | 字段 += 数值×施法者Attack |
| `数值变化衰减` | 数值变化/数值变化衰减.cs | 数值随剩余时长衰减 | GetValue | 字段 += 数值×Duration/总时长 |
| `m3融毁加攻` | Sp/m3融毁加攻.cs | m3 开启进度比例加攻 | Init/ApplyToUnit | AttackRate；BreakAllCast |

### B.2 关键机制注解

- **状态类 Buff 写法**：`Unit.UpdateBuffs()` 每帧先复位（`IfHide/IfSleep/IfSelectable/CanStopOther/IfStun = false`）再重放全部 Buff 的 `ApplyToUnit/Update`，因此状态 Buff 只需"置位"即可，无需自管清除。
- **数值类 Buff 写法**：在 `ApplyToUnit()` 中累加 `Unit.AttackAdd/AttackRate/DefenceAdd...` 等修正字段，由 `Unit.Refresh()` 统一重算最终属性（`(Base+Add)×(1+Rate)+AddFin)×(1+RateFin)`）。
- **伤害改写分层**：
  - `IDamageModify`（受击方 Buff）/ `ISelfDamageModify`（攻击方 Buff）：在 `Skill.GetDamageInfo` 构造伤害面板时改 `DamageRate/Attack/DefIgnore/Avoid...`。
  - `IDamageRewrite`（`Unit.DamageRewrites`，按 `OrderCode` 升序）：在 `FinalDamage` 算完后、扣血前改写最终值；`Avoid == true` 时整段跳过。
- **伤害类型参考**：`中毒/线性剧毒` 支持 `DamageBase` 0/1/2（固定值 / ×施法者 Attack / ×自身 MaxHp）；`剧毒` 固定 `LoseHP` 类型且随时间加速。
- **Buff抵挡（入梦砖）链路**：目标挂 `Buff抵挡` + 来源挂 `Buff可抵挡` → 新 buff 进 `Buff.Init` 时比较时长：短于抵挡时长直接 `Finish`（被挡），更长则延迟入列（`isBlocking`），抵挡结束时按 `{Id, Skill, Index, 剩余时长}` 重放。
- **数值取值约定**：`Skill.SkillData.GetBuffData(Index)` 按施加序号取 `BuffData / BuffData2 / BuffData3` 数组（`Config/Ex/SkillDataEx.cs`）；多数 buff 用 `BuffData.Data` 字典键（`t/v/Count/Rate/...`）取参数。

### B.3 值得注意的问题

1. 疑似重复类：`伤害重写.cs` ≡ `吸收类限伤.cs`（逐字节相同）；`设置属性.cs` ≈ `高级属性设置.cs`（几乎相同）。
2. 文件名与类名不一致：`数值变化自来源.cs` 内是 `Buffs.鼓舞`（配置 Type 必须写 `鼓舞`）。
3. 潜在 bug：`眩晕.Finish` 不恢复 `CanStopOther`；`覆盖动作.Finish` 不清 `OverWriteMove`；`麻痹.Init` 局部变量遮蔽字段；`数值变化衰减` 依赖 `SkillData.BuffLastTime` 非空；`数值变化叠加` 复制了基类 `Update` 逻辑（未调 base，基类升级易失同步）。
4. 接口实现互斥：没有任何一个 Buff 同时实现 `IDamageRewrite` 与 `IDamageModify`。

---

## 附录 C：Bullet / Modify / Map / View / Effect 子类全表

### C.1 子弹系统（`Bullet/`）

**基类 `Bullet`（纯逻辑类）**：字段 `Id / Skill / Target / TargetPos / Position / Direction / Speed(SpeedBase/Rate/Add) / Attack(AttackBase/Rate/Add/AddFin/RateFin) / Modifies / Buffs / BulletModel`。生命周期：`Init`（建模型、读 `BulletData.Modifys` 反射建修饰器并按 `OrderCode` 排序）→ `Update`（`UpdateBulletAttr` 刷 Buff 与数值）→ 命中时由子类调 `Skill.Hit(target, this)` 结算（子弹 `Attack` 作为倍率乘入 `DamageRate`，子弹修饰器按 `IBulletDamageModify.Modify(dInfo, bullet)` 注入）→ `Finish`（归还模型池）。`BulletManager` 单例持有 `Pool<BulletModel>` 与 `Pool<PullLineModel>`。

| 类名 | 一句话作用 | 关键逻辑 |
|---|---|---|
| `子弹` | 直线/抛物线追踪弹 | `moveHeight` 0=直线、>0=抛物线；每帧刷新 `TargetPos`，按 `totalTime=距离/Speed` 插值；到达 `Skill.Hit` 并 `Finish` |
| `持续施法子弹` | 持续施法激光 | Init 置施法者施法状态并生成 `PullLine`；每 `Cooldown` 触发一次 `Skill.Hit`；施法者死亡/眩晕/目标死亡即结束 |
| `冲击波` | 向目标点推进的波 | 前进同时检测路径上 0.25 半径内目标，命中即 `Skill.Hit` |
| `大飞镖` | 范围持续伤害弹 | 半径内按 `Trigger` 间隔结算，`DamagedUnits` 防重复、`MaxTargetCount` 限量、`TriggerTimes` 限次；支持额外目标 `ExTarget` |
| `延迟打击` | 延迟后对锁定位置打击 | `CountDown Delay` 结束后 `Skill.Hit`（目标空则打坐标） |
| `棘刺2` | 半径随时间扩张的范围伤害 | 大飞镖变体：半径指数扩张到 `MaxRadius`；用 `ShowRange` 圈表现 |
| `钩子` | 钩子线+命中 | 到达后先归还弹体模型但保留自身，`Skill.Hit` 并从 `(Skill as Skills.拉).pull` 取拉动 Buff，此后仅跟随 `PullLine` 终点 |
| `链式弹道` | 命中后链向下一个目标 | 用临时 Unit 学索敌技能找下一目标，`MaxLinkNum` 限链数，`LinkNum` 供衰减（配合修饰器 `链奶伤害衰减`） |

### C.2 修饰器系统（`Modify/`）

**基类 `Modify`**（定义于 `IModify.cs`）：`Id / ModifyData / Skill / orderCode`。工厂 `ModifyManager.Get(id, skill|bullet)` 反射创建 `Modifys.<Type>`（每次新实例）。接口族与调用点见 §7.2。

| 类名 | 作用 | 接口 | 改动 |
|---|---|---|---|
| `暴击` | 概率暴击倍率 | IDamageModify | 开技中按 `Chance1` 否则按 `Chance`，命中 `DamageRate *= Rate` |
| `穿甲` | 概率无视防御 | IDamageModify | `DefIgnore += Value`、`DefIgnoreRate += Rate` |
| `对低血伤害` | 目标血越少伤越高 | IDamageModify | `DamageRate *= 1 + Rate*(MaxHp-Hp)/MaxHp` |
| `对飞行伤害` | 对飞行增伤 | IDamageModify | `Target.Height > 0` 时 `DamageRate *= Rate` |
| `对远程伤害` | 对远程增伤 | IDamageModify | 目标 `FirstSkill.AttackRange > 0` 时增伤 |
| `未阻挡伤害` | 对未阻挡增伤 | IDamageModify | 干员且目标不在 `StopUnits` 时增伤 |
| `额外伤害加` | 概率附加伤害 | IDamageModify | 概率命中 `DamageRate += Rate` |
| `额外目标` | 概率加目标数 | ITargetModify | `count += Count` |
| `额外目标与伤害` | 目标数与伤害联动 | ITargetModify+IDamageModify | 命中时 `count += Count`，随后 `DamageRate *= Rate` |
| `额外目标依层数` | 按目标叠层加目标数 | ITargetModify | 目标有 `数值变化叠加` buff 时 `count += buff.Level` |
| `最大生命伤害` | 按目标 MaxHp 附加攻击 | IDamageModify | `Attack += Target.MaxHp * Rate` |
| `对buff伤害` | 目标带指定 buff 增伤 | IDamageModify | 目标含 `ModifyData.Buff` 时增伤 |
| `对个数伤害` | 命中个数恰为 N 增伤 | IDamageModify | `AllCount == Count` 时增伤 |
| `对buff回复技力` | 目标带 buff 回 SP | IUnitModify | 目标技能 `RecoverPower(spCount, withTip, ignoreOpening)` |
| `蓄力增伤` | 距上次攻击越久伤越高 | IDamageModify | `t = clamp(time/Time, 0, 1)`，`DamageRate *= 1 + t*(Rate-1)` |
| `链奶伤害衰减` | 链式弹道逐跳衰减 | IBulletDamageModify | `DamageRate *= Rate^LinkNum` |
| `治疗量提升依溢出` | 溢出治疗转下次加成 | IBulletDamageModify | `Attack += LastExDamage*Rate/DamageRate`，记录溢出 |

### C.3 地图系统（`Map/`）

- `Map`：持有 `Tile[,] Tiles` 与包围盒；`Init` 从场景 `MapGrid` 收集格子建 `Tile`（空位补默认）。
- `Tile : ITileData`：`CanBuildUnit / FarAttackGrid（高台）/ Units / MidUnits / passable / PassCost`；`CanSet(UnitData)` 部署判定（高台/地面、单位占用标签、`仅在单位攻击范围内:名` 特殊部署位）。
- `ITileData`：`Passable / PassCost` 两个只读属性，把寻路与 Tile 解耦。
- `PathFinder_AStar`：泛型 A*（4 方向、线性扫最小 F、`MaxSearchSteps=10000` 防死循环），支持途经点分段、`isFly` 直飞、`SmoothPath`（胶囊射线简化）+ 贝塞尔拐角平滑。
- `MapUnitInfo`：场景预置单位出生数据（`Time/Tag/Id/Pos/Direction/LifeTime`）。

### C.4 表现层（`View/`）

| 类 | 职责 |
|---|---|
| `BattleManager` | 全局单例：固定步长驱动 `Battle.Update`、战斗开始/结束、作弊开关（无限费用/血量/CD/部署）、伤害统计 `OpDamageInfos` |
| `UnitModel` | 单位视图基类：`GetPoint`（骨骼点）、`GetSkillDelay`（读 OnAttack 事件时间）、染色 `SetColor/ResetColor`、飘字 `ShowCrit/ShowHeal/ShowPower`、模型显隐 |
| `SpineModel` | Spine 模型：`LateUpdate` 轮询 `Unit.GetAnimation()/AnimationSpeed/ScaleX/Height/Position` 驱动动画；`GetPoint` 按骨骼名取世界坐标 |
| `PlayerUnitModel` | 干员双面模型：按 `Direction` 夹角切换正/背两套 Skeleton 与 `F_/B_` 骨骼 |
| `BulletModel` | 子弹视图：跟随 `Bullet.Position`、朝向投影到相机平面 |
| `MapTile` | 高亮格子视图（伤害/治疗双样式），供攻击范围显示与 A* 调试 |
| `PitModel` | 坑洞视图（隐藏格子 Mesh、下沉碰撞体） |
| `PullLineModel` | 连线视图（持续施法光束、钩子线） |
| `TrailManager` | 路径拖尾预览（波次路线） |
| `BattleCamera` | 主相机：部署预览、聚焦单位 + 攻击范围高亮、全图高亮、模糊 |
| `NormalModel` | 非 Spine 单位（Animator + MeshRenderer），按 `Ablititys` 染色 |

### C.5 特效（`Effect/`）

- `Effect : MonoBehaviour`：粒子/拖尾缓存、`LifeTime`、`ParentFollow`（1 挂父/2 随 user/3 随位置）、`BoneFollow`（BoneFollower 绑 `F_/B_+BindPoint`）、`ScaleXFollow`、朝向控制。两个 `Init` 重载：`(Unit user, Unit target, pos, dir, speed)` 与 `(Bullet target, speed)`。
- `EffectManager`（`Assets/Scripts/VFX/`）：单例对象池 `Pool<Effect>`，`GetEffect(id)/ReturnEffect/ReturnAll`。
- `旧Effect.cs`（已废弃副本）、`PullEffect.cs`（整文件注释的死代码）。

### C.6 Core 根目录其余文件

| 文件 | 职责 |
|---|---|
| `OneWave` | 单波次调度条目 `{Time, WaveData}` |
| `CountDown` | 通用倒计时工具 |
| `Construction` | 建造单位配置 `{Cost, BuildTime, UnitId}` |
| `Preview` | 地图编辑器波次敌人路径回放（FairyGUI 滑条，处理 Delay/HideMove/倍速/循环） |
| `SystemConfig` | 固定步长 `DeltaTime = 1/60f`、`TurningTime` |
| `BattleInput` | 开战配置（地图/种子/合约/编队或肉鸽） |


