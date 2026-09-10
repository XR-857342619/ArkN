using UnityEngine;

namespace Bullets
{
    /// <summary>
    /// 使用比例导引法追踪目标的子弹类型。
    /// 在 BulletData 的 Data 中可配置：
    /// NavigationConstant：比例导引常数，默认 4
    /// MaxTurnRate：最大转向速率（度/秒），0 不限制，默认 1440
    /// HomingHitRange：命中判定半径，优先使用；未配置时回退到 HitRange
    /// HitRange：命中判定半径，默认 0.1
    /// HomingDelay：追踪启动延迟，秒；默认 0，表示发射后立刻追踪
    /// LifeTime：存活时间，0 表示无限，默认 0
    /// </summary>
    public class 追踪子弹 : Bullet
    {
        CountDown LifeTime = new CountDown();
        float navigationConstant = HomingNavigation.DefaultNavigationConstant;
        float maxTurnRate;
        float hitRange = 0.1f;
        float minTurnRadius;
        float homingDelay;
        float homingDelayTimer;
        float lifeTime;
        bool finished;

        public override void Init()
        {
            base.Init();

            var data = BulletData.Data;
            navigationConstant = data.GetFloat("NavigationConstant", HomingNavigation.DefaultNavigationConstant);
            // 默认给一个非零水平转向速率，避免没有配置时换目标第一帧直接锐角转向。
            maxTurnRate = data.GetFloat("MaxTurnRate", 1440f);
            hitRange = data.GetFloat("HomingHitRange", data.GetFloat("HitRange", 0.1f));
            minTurnRadius = HomingNavigation.CalculateMinTurnRadius(BulletData.Speed, maxTurnRate);
            homingDelay = Mathf.Max(0f, data.GetFloat("HomingDelay", 0f));
            homingDelayTimer = homingDelay;
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

            Vector3 next;
            if (homingDelayTimer > 0f)
            {
                // 追踪启动延迟内只沿当前方向直线飞行。
                homingDelayTimer -= SystemConfig.DeltaTime;
                if (homingDelayTimer < 0f)
                    homingDelayTimer = 0f;

                next = CalculateHomingDelayPosition();
            }
            else
            {
                next = HomingNavigation.GetNextPosition(this, SystemConfig.DeltaTime, navigationConstant, maxTurnRate, minTurnRadius);
            }

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

        private Vector3 CalculateHomingDelayPosition()
        {
            Vector3 direction = Direction;

            if (direction.sqrMagnitude <= 0.0001f &&
                (TargetPos - Position).sqrMagnitude > 0.0001f)
            {
                direction = (TargetPos - Position).normalized;
            }

            if (direction.sqrMagnitude <= 0.0001f)
                return Position;

            direction.Normalize();
            Direction = direction;
            return Position + direction * BulletData.Speed * Speed * SystemConfig.DeltaTime;
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
