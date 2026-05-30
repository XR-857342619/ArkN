using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

public class Bullet
{
    public BulletData BulletData => Database.Instance.Get<BulletData>(Id);
    public int Id;

    public BulletModel BulletModel;

    public Vector3 StartPosition;
    public Vector3 Position;
    public Vector3 Direction;

    protected Battle Battle => Skill.Unit.Battle;
    public Skill Skill;
    public Unit Target;
    public Vector3 TargetPos;

    public List<Modify> Modifies = new List<Modify>();
    public List<Buff> Buffs = new List<Buff>();

    public float Speed;
    public float SpeedBase, SpeedRate, SpeedAdd;

    public float Attack;
    public float AttackBase, AttackRate, AttackAdd, AttackRateFin, AttackAddFin;

    public virtual void Init()
    {
        StartPosition = Position;
        CreateModel();
        //Log.Debug("加载弹道附加效果" + BulletData.Modifys.Length);
        if (BulletData.Modifys != null)
        {
            for (int i = 0; i < BulletData.Modifys.Length; i++)
            {
                //Log.Debug("加载弹道附加效果");
                //Debug.Log(BulletData.Modifys[i]);
                Modifies.Add(ModifyManager.Instance.Get(BulletData.Modifys[i], this));
            }
        }
        Modifies.Sort((x, y) => x.OrderCode.CompareTo(y.OrderCode));
        SpeedBase = 1;
        AttackBase = 1;
    }

    public virtual void CreateModel()
    {
        BulletModel = BulletManager.Instance.Get(BulletData.Model); //ResHelper.Instantiate(PathHelper.EffectPath + BulletData.Model).GetComponent<BulletModel>();
        BulletModel.GetComponent<Effect>().SetLifeTime(float.PositiveInfinity);
        BulletModel.Init(this);
    }

    public virtual void Update()
    {
        UpdateBulletAttr();
    }

    public virtual void Finish()
    {
        Battle.Bullets.Remove(this);
        if (BulletModel != null)
        {
            BulletManager.Instance.Return(BulletModel);
            BulletModel = null;
        }
        //Battle.TriggerDatas.Push(new TriggerData()
        //{
        //    User = Skill.Unit,
        //    Skill = Skill,
        //    Target = Target,
        //});
        //Battle.Trigger(TriggerEnum.弹道命中);
        //Battle.TriggerDatas.Pop();
        //GameObject.Destroy(BulletModel.gameObject);
    }
    public Vector3 GetTargetPos(Unit target)
    {
        //Log.Debug(target.UnitData.Name);
        //Log.Debug(target.UnitData.Id);
        if (BulletData.EffectBase == 0)
            return target.GetHitPoint();
        else
            return target.UnitModel.transform.position;
    }

    public virtual void UpdateBulletAttr()
    {
        foreach (var buff in Buffs)
        {
            if (buff.Enable()) buff.ApplyToBullet();
        }
        Speed = Math.Max(0.001f, (SpeedBase + SpeedAdd) * (1 + SpeedRate));
        Attack = Math.Max(1, ((AttackBase + AttackAdd) * (1 + AttackRate) + AttackAddFin) * (1 + AttackRateFin));
    }

    public Buff AddBuff(int buffId, Skill source, int index, float lastTime = -1.0f)
    {
        //if (IgnoreBuffs.Contains(buffId)) return null;

        var config = Database.Instance.Get<BuffData>(buffId);
        //Debug.Log("AddBuff:" + config.Id);
        if (config.RelyBuff != null && !Buffs.Any(x => x.Id == config.RelyBuff.Value))
            return null;

        // 检查是否存在buff的升级版
        var oldBuff = Buffs.FirstOrDefault(x => (x.Id == buffId || config.Upgrade == x.Id) && (config.UnSourceCheck || x.Skill == Skill));
        if (oldBuff != null)
        {
            oldBuff.Reset();
            return oldBuff;
        }
        else
        {
            // 创建新的buff实例
            var newBuff = typeof(Buff).Assembly.CreateInstance(nameof(Buffs) + "." + config.Type) as Buff;
            if (newBuff == null)
            {
                TipManager.Instance.ShowTip("创建" + config.Id + "Buff失败, 请检查" + config.Type + "是否存在");
                return null;
            }
            newBuff.Index = index;
            newBuff.Id = buffId;
            newBuff.Skill = source;
            //newBuff.Unit = this;
            newBuff.LastTime = lastTime;

            //Debug.Log("Add " + config.Id + " Buff to " + UnitData.Name);

            // 添加到BUFF列表
            Buffs.Add(newBuff);

            //// 如果伤害重写类型，添加到伤害重写列表
            //if (newBuff is IDamageRewrite shield)
            //{
            //    DamageRewrites.Add(shield);
            //    DamageRewrites.SortTargets((a, b) => a.OrderCode.CompareTo(b.OrderCode));
            //}

            // 初始化BUFF
            newBuff.Init();

            return newBuff;
        }
    }

    public void RemoveBuff(Buff buff)
    {
        Buffs.Remove(buff);
        //if (buff is IDamageRewrite shield) DamageRewrites.Remove(shield);
        //Refresh();
    }
}

