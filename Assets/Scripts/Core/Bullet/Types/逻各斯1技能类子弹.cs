using UnityEngine;

namespace Bullets
{
    public class 逻各斯1技能类子弹 : Bullet
    {
        private float moveHeight; // 原变量名: a2rs
        private float elapsedTime; // 原变量名: a2rh
        public float LogosBulletAttack; // 特殊攻击力属性

        public override void Init()
        {
            base.Init();

            // 目标存在且存活时，更新目标位置
            if (Target != null && Target.Alive())
            {
                TargetPos = GetTargetPos(Target);
            }

            // 获取子弹移动高度参数
            moveHeight = BulletData.Data.GetFloat("MoveHeight", 0f);

            // 根据子弹面向相机的方式调整方向或旋转
            if (moveHeight == 0f && BulletData.FaceCamera == 2)
            {
                Direction = TargetPos - Position;
            }

            if (BulletData.FaceCamera == 1)
            {
                BulletModel.transform.eulerAngles = new Vector3(60f, 0f, 0f);
            }

            // 设置子弹缩放
            float scaleX = 1f;
            if (BulletData.ScaleX == 1)
            {
                scaleX = Target.ScaleX;
            }
            if (BulletData.ScaleX == 2)
            {
                scaleX = Skill.Unit.ScaleX;
            }
            BulletModel.transform.localScale = new Vector3(scaleX, 1f, 1f);
        }

        public override void Update()
        {
            elapsedTime += SystemConfig.DeltaTime;

            // 更新目标位置（如果目标存在且存活）
            if (Target != null && Target.Alive())
            {
                TargetPos = GetTargetPos(Target);
            }

            // 计算新位置
            Position = CalculatePosition(elapsedTime);
            Direction = TargetPos - Position;
        }

        private Vector3 CalculatePosition(float time)
        {
            // 计算总飞行时间
            float totalTime = (TargetPos - StartPosition).magnitude / BulletData.Speed;

            // 如果时间超过总飞行时间，表示子弹到达目标
            if (time > totalTime)
            {
                Position = TargetPos;// 原方法名: rhi

                // 处理命中逻辑 - 添加目标存活检查
                if (Target != null && Target.Alive())
                {
                    // 对目标造成伤害，使用LogosBulletAttack作为伤害值
                    Skill.Hit(Target, LogosBulletAttack, this, true);
                    Log.Debug("{ LogosBulletAttack }");
                    Finish();
                }
                else
                {
                    // 目标已死亡或不存在，可以添加一些视觉效果或日志
                    Finish();
                }
                return Position;
            }

            // 计算线性插值位置
            Vector3 result = StartPosition + (TargetPos - StartPosition) * (time / totalTime);

            // 添加抛物线高度（如果有）
            if (moveHeight > 0f)
            {
                float t = time / totalTime;
                result.y += (-5f * t * t + 5f * t) * moveHeight;
            }

            return result;
        }
    }
}