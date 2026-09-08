using UnityEngine;

namespace Bullets
{
    /// <summary>
    /// 使用比例导引法追踪目标的子弹类型。
    /// 在 BulletData 的 Data 中可配置：
    /// NavigationConstant：比例导引常数，默认 4
    /// MaxTurnRate：最大转向速率（度/秒），0 不限制，默认 0
    /// HitRange：命中判定半径，默认 0.1
    /// LifeTime：存活时间，0 表示无限，默认 0
    /// </summary>
    public class 追踪子弹 : Bullet
    {
        CountDown LifeTime = new CountDown();
        float navigationConstant = HomingNavigation.DefaultNavigationConstant;
        float maxTurnRate;
        float hitRange = 0.1f;
        float lifeTime;
        bool finished;

        public override void Init()
        {
            base.Init();

            var data = BulletData.Data;
            navigationConstant = data.GetFloat("NavigationConstant", HomingNavigation.DefaultNavigationConstant);
            maxTurnRate = data.GetFloat("MaxTurnRate", 0f);
            hitRange = data.GetFloat("HitRange", 0.1f);
            lifeTime = data.GetFloat("LifeTime", 0f);
            if (lifeTime > 0f)
                LifeTime.Set(lifeTime);

            if (Target == null || !Target.Alive())
            {
                Finish();
                return;
            }

            TargetPos = HomingNavigation.GetTargetPosition(this, Target);
            Direction = TargetPos - Position;

            if (BulletData.FaceCamera == 1)
                BulletModel.transform.eulerAngles = new Vector3(60, 0, 0);

            float scaleX = 1f;
            if (BulletData.ScaleX == 1)
                scaleX = Target.ScaleX;
            else if (BulletData.ScaleX == 2)
                scaleX = Skill.Unit.ScaleX;
            BulletModel.transform.localScale = new Vector3(scaleX, 1, 1);
        }

        public override void Update()
        {
            if (finished)
                return;

            base.Update();

            if (lifeTime > 0f && LifeTime.Update(SystemConfig.DeltaTime))
            {
                Finish();
                return;
            }

            if (Target == null || !Target.Alive())
            {
                Finish();
                return;
            }

            TargetPos = HomingNavigation.GetTargetPosition(this, Target);
            Vector3 next = HomingNavigation.GetNextPosition(this, SystemConfig.DeltaTime, navigationConstant, maxTurnRate);
            float step = Mathf.Max(hitRange, BulletData.Speed * Speed * SystemConfig.DeltaTime);

            // 到达目标或本帧步长已足够跨越目标时，判定命中。
            if ((next - TargetPos).sqrMagnitude <= step * step)
            {
                Position = TargetPos;
                if (Target.Alive())
                    Skill.Hit(Target, this);
                Finish();
                return;
            }

            Position = next;
        }

        public override void Finish()
        {
            if (finished)
                return;
            finished = true;
            base.Finish();
        }
    }
}
