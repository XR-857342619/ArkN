using System;
using UnityEngine;

namespace Buffs
{
    /// <summary>
    /// 重设高度：起飞 → 维持 → 降落 三阶段
    /// 配置项（BuffData.Data 字典）：
    ///   Start    起飞前高度（默认 0，即地面；降落后固定回到地面）
    ///   Fly      起飞后目标高度（默认 1.0）
    ///   Time     完成上升/下降所需时间（默认 0.1 s）
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
        private bool isTakingOff = true; // 起飞阶段
        private bool isLanding;          // 降落阶段
        private bool isLanded;           // 已降落完成，避免重复触发降落

        // 阶段计时：保证上升/下降在 smoothTime 内完成
        private float phaseStartHeight;
        private float phaseElapsed;

        public override void Init()
        {
            base.Init();

            startHeight = Unit.Height;
            takeOffHeight = BuffData.Data.GetFloat("Fly", 1f);
            smoothTime = Mathf.Max(0.001f, BuffData.Data.GetFloat("Time", 0.1f));

            // 移除 Land 参数：降落后回到起飞前的高度
            landingHeight = startHeight;

            Unit.Height = startHeight;

            isTakingOff = true;
            isLanding = false;
            isLanded = false;
            phaseStartHeight = startHeight;
            phaseElapsed = 0f;
        }

        public override void ApplyToUnit()
        {
            base.ApplyToUnit();

            if (Dead)
                return;

            phaseElapsed += SystemConfig.DeltaTime;
            float rawT = Mathf.Clamp01(phaseElapsed / smoothTime);
            float t = Mathf.SmoothStep(0f, 1f, rawT);

            if (isTakingOff)
            {
                Unit.Height = Mathf.Lerp(phaseStartHeight, takeOffHeight, t);

                if (rawT >= 1f)
                {
                    Unit.Height = takeOffHeight;
                    isTakingOff = false;
                }
            }
            else if (isLanding)
            {
                Unit.Height = Mathf.Lerp(phaseStartHeight, landingHeight, t);

                if (rawT >= 1f)
                {
                    Unit.Height = landingHeight;
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

            // 剩余时间不足一个阶段时长时开始降落；已经降落完成则不再重复触发
            if (!isTakingOff && !isLanding && !isLanded && Duration.value <= smoothTime)
            {
                isLanding = true;
                phaseStartHeight = Unit.Height;
                phaseElapsed = 0f;
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
