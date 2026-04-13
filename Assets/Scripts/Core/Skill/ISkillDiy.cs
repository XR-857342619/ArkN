using System.Collections;
using System.Collections.Generic;
using System.Numerics;
using UnityEngine;

public interface ITargetSelector
{
    public List<T> GetTargets<T>(Skill skill, List<T> targets)
    {
        return targets;
    }
}

public interface ITargetSorter
{
    public List<T> Sort<T>(Skill skill, List<T> targets)
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
    public ExecutionResult Execute<T>(Skill skill, List<T> targets)
    {
        return new ExecutionResult();
    }
}
