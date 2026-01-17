using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Bullets
{
    public class 大飞镖 : Bullet
    {
        HashSet<Unit> DamagedUnits = new HashSet<Unit>();
        //HashSet<Unit> damageUnits = new HashSet<Unit>();

        bool arrive;
        CountDown LifeTime;
        CountDown TriggerTime = new CountDown();
        float radius;

        float moveHeight;//0:直线 1:抛物线 2.瞬移 3.静止
        float tickTime;
        int targetTeam = -1;
        int maxTargetCount = -1;
        int triggerTimes = -1;
        //float attackGap = -1;
        object tmp;
        bool countLimit = false;
        bool startAttack = false;
        public override void Init()
        {
            base.Init();
            if (Target.Alive())
                TargetPos = GetTargetPos(Target);
            moveHeight = BulletData.Data.GetFloat("MoveHeight");
            LifeTime = new CountDown(BulletData.Data.GetFloat("_lifeTime"));
            radius = BulletData.Data.GetFloat("Radius");
            if (BulletData.Data.TryGetValue("TargetTeam", out tmp))
                targetTeam = Convert.ToInt32(tmp);
            //if (BulletData.Data.TryGetValue("MaxTargetCount", out tmp))
            //{
            //    countLimit = true;
            //    maxTargetCount = Convert.ToInt32(tmp);
            //}
            maxTargetCount = BulletData.Data.GetInt("MaxTargetCount",-1);
            countLimit = maxTargetCount != -1;
            Debug.Log("maxTargetCount:" + maxTargetCount);
            Debug.Log("countLimit:" + countLimit);
            if (BulletData.Data.TryGetValue("TriggerTimes", out tmp))
            {
                //Debug.Log("触发次数:" + tmp);
                //Debug.Log(tmp is int);
                triggerTimes = Convert.ToInt32(tmp);
            }
            //Debug.Log("高度:" + moveHeight);
            //if (maxTargetCount != -1)
            //countLimit = true;
            //if (BulletData.Data.TryGetValue("AttackGap", out tmp))
            //    attackGap = Convert.ToSingle(tmp);
            if (moveHeight == 0 && BulletData.FaceCamera == 2) Direction = TargetPos - this.Position;
            if (BulletData.FaceCamera == 1) BulletModel.transform.eulerAngles = new Vector3(60, 0, 0);
            float scaleX = 1;
            if (BulletData.ScaleX == 1) scaleX = Target.ScaleX;
            if (BulletData.ScaleX == 2) scaleX = Skill.Unit.ScaleX;
            BulletModel.transform.localScale = new Vector3(scaleX, 1, 1);
        }
        public override void Update()
        {
            base.Update();
            tickTime += SystemConfig.DeltaTime;
            if (Target.Alive())
                TargetPos = GetTargetPos(Target);
            if (!arrive)
            {
                if (moveHeight == 0)
                {
                    Position = getPosOfTime(tickTime);
                }
                else if (moveHeight == 2)
                {
                    Position = TargetPos;
                    arrive = true;
                }
                else
                {
                    Position = getPosOfTime(tickTime);
                    if (BulletData.FaceCamera == 2)
                        Direction = getPosOfTime(tickTime + SystemConfig.DeltaTime) - Position;
                }
            }
            ////if (DamagedUnits.Count > 0 && TriggerTime.Finished())
            //if (TriggerTime.Finished())
            //{
            //    TriggerTime.Set(BulletData.Data.GetFloat("Trigger"));
            //}
            int team = targetTeam == -1 ? Skill.SkillData.TargetTeam : targetTeam;
            var targets = Battle.FindAll(Position.ToV2(), radius, team);
            targets.UnionWith(Battle.FindAll(Position, radius, 7).Where(x => x.UnitData.Name == Skill.SkillData.Data.GetStr("ExTarget")));
            if (targets.Count > 0 && !startAttack)
            {
                TriggerTime.Set(BulletData.Data.GetFloat("Trigger"));
                startAttack = true;
            }
            if (TriggerTime.Update(SystemConfig.DeltaTime))
            {
                if (triggerTimes != -1)
                {
                    triggerTimes--;
                }
                DamagedUnits.Clear();
                TriggerTime.Set(BulletData.Data.GetFloat("Trigger"));
            }
            //Debug.Log("target team:" + team);
            foreach (var t in targets)
            {
                if (!DamagedUnits.Contains(t))
                {
                    //Debug.Log("击中:" + t.UnitData.Id);
                    if (countLimit && maxTargetCount == 0)
                        break;
                    DamagedUnits.Add(t);
                    if (countLimit)
                    {
                        if (maxTargetCount > 0)
                        {
                            //Log.Debug("maxTargetCount:" + maxTargetCount);
                            maxTargetCount--;
                            Skill.Hit(t, this);
                        }
                        else
                        {
                            break;
                        }
                    }
                    else
                        Skill.Hit(t, this);
                    //Log.Debug(t.UnitData.Name);
                }
            }
            if (LifeTime.Update(SystemConfig.DeltaTime))
            {
                Finish();
            }
            if (arrive && BulletData.Data.GetFloat("_lifeTime")==0)
            {
                Finish();
            }
            if (triggerTimes == 0)
            {
                Finish();
            }
        }

        Vector3 getPosOfTime(float time)
        {
            Vector3 position = Vector3.zero;
            float totalTime = (TargetPos - StartPosition).magnitude / BulletData.Speed * Speed;
            if (time > totalTime)
            {
                position = TargetPos;
                arrive = true;
            }
            else
                position = StartPosition + (TargetPos - StartPosition) * (time / totalTime);
            if (moveHeight == 1 || moveHeight == 2)
            {
                float t = time / totalTime;
                position.y += (-5 * t * t + 5 * t) * moveHeight;
            }
            if (moveHeight == 3)
            {
                position = StartPosition;
            }
            return position;
        }
    }
}
