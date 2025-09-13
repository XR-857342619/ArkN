using System.Collections.Generic;
using UnityEngine;

namespace Bullets
{
    public class 链式弹道 : Bullet
    {
        float moveHeight;//0:直线 1:抛物线
        float tickTime;
        string skillId;
        public int maxLinkNum;
        public int linkNum;
        public float reductionRate;
        public float reductionBase;
        public List<Unit> LinkedTargets;
        public bool canBack;
        public List<Unit> UsedTarget = new List<Unit>();
        public Unit lastTaget;
        public Unit tmp;
        public Skill findTargetSkill;
        public override void Init()
        {
            base.Init();
            if (Target!=null&& Target.Alive())
                TargetPos = GetTargetPos(Target);
            moveHeight = BulletData.Data.GetFloat("MoveHeight");
            maxLinkNum = BulletData.Data.GetInt("MaxLinkNum");
            linkNum = 0;
            reductionBase = BulletData.Data.GetFloat("ReductionRate",0);
            reductionRate = 1 - reductionBase * linkNum;
            canBack = BulletData.Data.GetBool("CanBack");
            skillId = BulletData.Data.GetStr("SkillId");
            //Debug.Log("高度:" + moveHeight);
            if (moveHeight == 0 && BulletData.FaceCamera == 2) Direction = TargetPos - this.Position;
            if (BulletData.FaceCamera == 1) BulletModel.transform.eulerAngles = new Vector3(60, 0, 0);
            float scaleX = 1;
            if (BulletData.ScaleX == 1) scaleX = Target.ScaleX;
            if (BulletData.ScaleX == 2) scaleX = Skill.Unit.ScaleX;
            BulletModel.transform.localScale = new Vector3(scaleX, 1, 1);
            tmp = Battle.CreateTempUnit(this.Position, (TargetPos - this.Position).ToV2());
            findTargetSkill = tmp.LearnSkill(Database.Instance.GetIndex<SkillData>(skillId));
            findTargetSkill.Init();
            //Debug.Log(findTargetSkill);
        }
        public override void Update()
        {
            tmp.Position = Position;
            //tmp.Direction = Direction.ToV2();
            tickTime += SystemConfig.DeltaTime;
            if (Target != null && Target.Alive())
                TargetPos = GetTargetPos(Target);
            else
            {
                Battle.Bullets.Remove(this);
                if (BulletModel != null)
                {
                    BulletManager.Instance.Return(BulletModel);
                    BulletModel = null;
                }
            }
            if (moveHeight == 0)
            {
                Position = getPosOfTime(tickTime);
            }
            else
            {
                Position = getPosOfTime(tickTime);
                if (BulletData.FaceCamera == 2)
                    Direction = getPosOfTime(tickTime + SystemConfig.DeltaTime) - Position;
            }
        }

        Vector3 getPosOfTime(float time)
        {
            Vector3 Postion;
            float totalTime = (TargetPos - StartPosition).magnitude / BulletData.Speed;
            Postion = StartPosition + (TargetPos - StartPosition) * (time / totalTime);
            if (moveHeight > 0)
            {
                float t = time / totalTime;
                Postion.y += (-5 * t * t + 5 * t) * moveHeight;
            }

            if (time > totalTime)
            {
                Position = TargetPos;
                tmp.Position = Position;
                if (Target == null)
                {
                    Debug.Log("弹道位置" + Position + " 索敌起始位置:" + tmp.Position);
                    Skill.Hit(TargetPos.ToV2(), this);
                }
                else if (Target != null && Target.Alive())
                {
                    Debug.Log("弹道位置" + Position + " 索敌起始位置:" + tmp.Position);
                    Skill.Hit(Target, this);
                }
                //if (maxLinkNum)
                maxLinkNum--;
                linkNum++;
                reductionRate = 1 - reductionBase * linkNum;
                UsedTarget.Add(Target);
                findTargetSkill.UpdateAttackPoints();
                findTargetSkill.FindTarget();
                if (maxLinkNum > 0 && findTargetSkill.Targets.Count > 0)
                {
                    if (!canBack)
                    {
                        Target = findTargetSkill.Targets.Find(x => x.Alive() && !UsedTarget.Contains(x));
                        if (Target != null)
                        {
                            TargetPos = GetTargetPos(Target);
                            StartPosition = Postion;
                            tickTime = 0;
                        }
                    }
                    else
                    {
                        var targets = findTargetSkill.Targets;
                        Target = targets.Find(x => x.Alive() && (targets.Count <= 1 ? true : x != lastTaget));
                        TargetPos = GetTargetPos(Target);
                        StartPosition = Postion;
                        tickTime = 0;
                    }
                }
                else
                {
                    findTargetSkill.HideUnitAttackArea();
                    findTargetSkill.Finish();
                    Battle.AllUnits.Remove(tmp);
                    Finish();
                }
            }
            return Postion;
        }
    }
}
