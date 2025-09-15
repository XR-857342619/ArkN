using System;
using UnityEngine;
using Buffs;

public class 重写类限伤 : Buff
{
    public int Count;
    public float RewriteDamage;

    public void Init()
    {
        base.Init();
        this.Count = base.BuffData.Data.GetInt("Count", 0);
        this.Unit.RewriteDamage = (float)this.Count;
    }
    public void Apply()
    {
        base.Apply();
        if (this.Dead)
        {
            return;
        }
        this.Unit.RewriteDamage = (float)this.Count;
    }
    public void Finish()
    {
        base.Finish();
        this.Unit.RewriteDamage = 0f;
    }

}