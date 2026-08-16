# 11 敌人寻路系统与 Debug 工具

> 本次重构将敌人类单位（`Units.敌人`）的寻路逻辑从 `敌人.cs` 抽离到独立文件 `Assets/Scripts/Core/Unit/EnemyPathfinder.cs`，并新增 `Assets/Scripts/Helper/PathDebugger.cs` 用于寻路流程调试。

## 1. 敌人寻路流程梳理

```
出生 Init
  └─ Pathfinder.Initialize(this, WaveData)
       ├─ 读取 Battle.MapData.PathInfos 中 WaveData.Path 对应路径
       ├─ 首尾路径点标记为 CheckPoint
       ├─ CheckPoints = PathPoints 中所有 CheckPoint
       ├─ Position = 第一个路径点坐标
       ├─ PathWaiting = 第一个路径点 Delay
       └─ ScaleX 朝向下一路径点

每帧 UpdateAction
  └─ UpdateMove
       ├─ Pathfinder.UpdateTempPoints(dt)          // 更新技能插入的临时路径点计时
       ├─ 若 PathWaiting 未结束且无临时路径点 -> Idle 等待
       ├─ Pathfinder.CheckArrival()                 // 是否到达 TempPath 下一目标点
       │     └─ 到达 TempPath 末尾时推进 currentPathIndex / currentCheckIndex
       │           ├─ 若到达最终点 -> 对关卡造成伤害并 Finish
       │           ├─ 否则 PathWaiting.Set(当前点 Delay)
       │           └─ 若 HideMove -> 进入隐藏移动
       ├─ 若 TempPath == null 或 NeedResetPath -> Pathfinder.FindNewPath(OnlyCheckPoint)
       │     ├─ 同一格或飞行单位 -> 直线
       │     ├─ 地面单位 -> AStarPathFinder.FindPath(...)
       │     └─ A* 返回空路径 -> 退化直线并告警
       ├─ 被阻挡 / 失衡 / 隐身中 -> 不移动
       └─ 沿 TempTarget 方向移动 Speed * dt
```

## 2. 关键文件与职责

| 文件 | 职责 |
|---|---|
| `Assets/Scripts/Core/Unit/EnemyPathfinder.cs` | 敌人寻路状态机：路径点维护、临时路径点插入/过期、A* 调用、到达检测、距离估算 |
| `Assets/Scripts/Core/Unit/Types/敌人.cs` | 敌人本体状态：出生、阻挡、动画、移动输入；寻路委托给 `Pathfinder` |
| `Assets/Scripts/Helper/PathDebugger.cs` | 寻路 Debug 工具：Scene 视图绘制路径/点、Console 日志 |
| `Assets/Scripts/Core/Map/PathFinder_AStar.cs` | A* 寻路算法（未改动） |

## 3. 本次修复的极端环境报错点

| 问题 | 说明 |
|---|---|
| A* 返回空路径 | 原 `TempPath` 可能为空列表，随后 `TempTarget` 访问 `TempPath[TempPath.Count - 1]` 越界。现 `FindNewPath` 对空结果退化为起终点直线。 |
| `TempTarget` 空路径访问 | `EnemyPathfinder.TempTarget` 增加空列表保护，回退到单位当前位置并告警。 |
| `NowCheckPoint` / `NextCheckPoint` 越界 | 对 `CheckPoints` 为空或下标越界的情况使用 `Mathf.Clamp` 并返回 null。 |
| `GetPoint` 越界 | 对 `OnlyCheckPoint` / `PathPoints` 访问增加边界判断，越界返回 null。 |
| `FinishHideMove` / `OnArriveAtPathPoint` 空引用 | 推进路径点前先判断 `NowPathPoint` / `NextPathPoint` 是否为 null。 |
| `PathInfo` 为空 | `Initialize` 中找不到波次路径时打日志并 return，避免后续空引用。 |

## 4. Debug 工具用法

### 4.1 PathDebugger 开关

```csharp
PathDebugger.Enabled = true;      // 总开关
PathDebugger.DrawEnabled = true;  // Scene 视图绘制开关
PathDebugger.LogEnabled = false;  // Console 路径日志开关（日志较多，按需开启）
```

可以在任意调试入口（如 `Init.Start` 或 Inspector 脚本）中打开。

### 4.2 Scene 视图绘制

- `PathDebugger.DrawPath(List<Vector3> path, Color color, float duration)`：绘制折线路径。
- `PathDebugger.DrawPoint(Vector3 position, Color color, float size, float duration)`：绘制路径点十字线。

`EnemyPathfinder.FindNewPath` 会以黄色绘制当前临时路径；`TryAddTempPoint` 会以青色绘制临时路径点；`DrawDebug` 方法会绘制完整状态（可挂在 OnDrawGizmos 中扩展）。

### 4.3 Console 日志

- `PathDebugger.Log(title, message)`
- `PathDebugger.LogPath(title, section, path)`

开启 `LogEnabled` 后，敌人每次寻路会输出起点、终点、临时路径点列表，便于定位异常。

## 5. 兼容性说明

`敌人.cs` 中旧字段与旧方法名已改为代理属性/方法，保留原访问方式，外部代码无需改动：

- `enemy.PathPoints` / `enemy.CheckPoints`
- `enemy.currentPathIndex` / `enemy.currentCheckIndex`
- `enemy.OnlyCheckPoint` / `enemy.NeedResetPath`
- `enemy.TempPath` / `enemy.TempIndex`
- `enemy.AddTmpPathPoint(pos, time)` / `enemy.IsCanArrive(start, end)`
- `enemy.DisplayPath()`
