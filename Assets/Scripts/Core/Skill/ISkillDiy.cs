using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public partial class SelectorConfig
{
    public bool DeadFind;
    public int TargetTeam;
}

public interface ITargetSelector
{
    public List<Unit> GetTargets(Skill skill, List<Unit> targets, SelectorConfig config)
    {
        return targets;
    }
    public List<Unit> GetTargets(Skill skill, List<Tile> targets, SelectorConfig config)
    {
        return new List<Unit>();
    }
    public List<Vector3> GetTargetsPos(Skill skill, List<Vector3> targets, SelectorConfig config)
    {
        return targets;
    }
    public List<Vector3> GetTargetsPos(Skill skill, List<Unit> targets, SelectorConfig config)
    {
        return new List<Vector3>();
    }
}

public interface ITargetSorter
{
    public partial class SorterConfig
    {

    }

    public List<Unit> Sort(Skill skill, List<Unit> targets, SorterConfig config)
    {
        return targets;
    }
    public List<Vector2> Sort(Skill skill, List<Vector2> targets, SorterConfig config)
    {
        return targets;
    }
}

public class ExecutionResult
{
    protected TriggerEnum Event;
    protected int Effect;
}

public interface IExecutor
{
    public partial class ExecutorConfig
    {
    
    }
    public ExecutionResult Execute<T>(Skill skill, List<T> targets)
    {
        return new ExecutionResult();
    }
}
