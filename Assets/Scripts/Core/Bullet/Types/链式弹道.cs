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
        public List<Unit> LinkedTargets;
        public bool canBack;
        public Unit LastTarget = null;
        protected Unit tmp = new Unit();
        public Skill findTargetSkill;
        public override void Init()
        {
            base.Init();
            if (Target!=null&& Target.Alive())
                TargetPos = GetTargetPos(Target);
            moveHeight = BulletData.Data.GetFloat("MoveHeight");
            maxLinkNum = BulletData.Data.GetInt("MaxLinkNum");
            canBack = BulletData.Data.GetBool("CanBack");
            skillId = BulletData.Data.GetStr("SkillId");
            //Debug.Log("高度:" + moveHeight);
            if (moveHeight == 0 && BulletData.FaceCamera == 2) Direction = TargetPos - this.Position;
            if (BulletData.FaceCamera == 1) BulletModel.transform.eulerAngles = new Vector3(60, 0, 0);
            float scaleX = 1;
            if (BulletData.ScaleX == 1) scaleX = Target.ScaleX;
            if (BulletData.ScaleX == 2) scaleX = Skill.Unit.ScaleX;
            BulletModel.transform.localScale = new Vector3(scaleX, 1, 1);
            findTargetSkill = tmp.LearnSkill(Database.Instance.GetIndex<SkillData>(skillId));
            findTargetSkill.Init();
        }
        public override void Update()
        {
            tmp.Position = Position;
            tmp.Direction = Direction;
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
            if (time > totalTime)
            {
                Position = TargetPos;
                if (Target == null)
                    Skill.Hit(TargetPos.ToV2(), this);
                else if (Target.Alive())
                    Skill.Hit(Target, this);
                //if (maxLinkNum)
                maxLinkNum--;
                LastTarget = Target;
                if (maxLinkNum > 0)
                {
                    findTargetSkill.FindTarget();
                    if (!canBack)
                    {
                        Target = findTargetSkill.Targets.Find(x => x.Alive() && x != LastTarget);
                    }
                    else
                    {
                        Target = findTargetSkill.Targets.Find(x => x.Alive());
                    }
                }
                else
                    Finish();
            }
            Postion = StartPosition + (TargetPos - StartPosition) * (time / totalTime);
            if (moveHeight > 0)
            {
                float t = time / totalTime;
                Postion.y += (-5 * t * t + 5 * t) * moveHeight;
            }
            return Postion;
        }
    }
}
