using System;
using UnityEngine;

namespace Buffs
{
    /// <summary>
    /// 重设高度：起飞 → 维持 → 降落 三阶段
    /// 配置项（BuffData.Data 字典）：
    ///   Start    起飞前高度（默认 0，即地面；降落后固定回到地面）
    ///   Fly      起飞后目标高度（默认 1.0）
    ///   Time     平滑阻尼时间（默认 0.1 s）
    /// 不再读取 Land 参数：降落后固定回到地面（0），并调用 AlignHeight 对齐地面。
    /// </summary>
    public class 重设高度 : Buff
    {
        // 配置数据
        private float startHeight;
        private float takeOffHeight;
        private float landingHeight;
        private float smoothTime;

        // 运行时状态
        private float velocity;          // SmoothDamp 用
        private bool isTakingOff = true; // 起飞阶段
        private bool isLanding;          // 降落阶段
        private bool isLanded;           // 已降落完成，避免重复触发降落

        public override void Init()
        {
            base.Init();

            startHeight = Unit.Height;
            takeOffHeight = BuffData.Data.GetFloat("Fly", 1f);
            smoothTime = Mathf.Max(0.001f, BuffData.Data.GetFloat("Time", 0.1f));

            // 移除 Land 参数：降落后回到地面
            landingHeight = startHeight;

            isTakingOff = true;
            isLanding = false;
            isLanded = false;
            velocity = 0f;
        }

        public override void ApplyToUnit()
        {
            base.ApplyToUnit();

            if (Dead)
                return;

            if (isTakingOff)
            {
                Unit.Height = Mathf.SmoothDamp(Unit.Height, takeOffHeight, ref velocity, smoothTime, Mathf.Infinity, SystemConfig.DeltaTime);
                if (Mathf.Abs(Unit.Height - takeOffHeight) < 0.01f)
                {
                    Unit.Height = takeOffHeight;
                    velocity = 0f;
                    isTakingOff = false;
                }
            }
            else if (isLanding)
            {
                Unit.Height = Mathf.SmoothDamp(Unit.Height, landingHeight, ref velocity, smoothTime, Mathf.Infinity, SystemConfig.DeltaTime);
                if (Mathf.Abs(Unit.Height - landingHeight) < 0.01f)
                {
                    Unit.Height = landingHeight;
                    velocity = 0f;
                    isLanding = false;
                    isLanded = true;

                    // 降落完成后让模型与当前地块对齐，避免悬空/穿模
                    Unit.UnitModel?.AlignHeight();
                }
            }
        }

        public override void Update()
        {
            base.Update();

            if (Dead)
                return;

            // 剩余时间不足一个平滑周期时开始降落；已经降落完成则不再重复触发
            if (!isTakingOff && !isLanding && !isLanded && Duration.value <= smoothTime)
            {
                isLanding = true;
                velocity = 0f;
            }
        }

        public override void Finish()
        {
            if (Unit != null)
            {
                Unit.Height = landingHeight;
                Unit.UnitModel?.AlignHeight();
            }

            base.Finish();
        }
    }
}
