using System;
using UnityEngine;

namespace Buffs
{
    /// <summary>
    /// 重设高度：起飞 → 维持 → 降落 三阶段
    /// 配置项：
    ///   StartHeight    起始高度（不填则保持当前高度）
    ///   TakeOffHeight  起飞后目标高度（默认 1.0）
    ///   LandingHeight  降落后目标高度（默认 0.0）
    ///   SmoothTime     平滑阻尼时间（默认 0.3 s）
    /// </summary>
    public class 重设高度 : Buff
    {
        // 配置数据
        private float startHeight;   // 新增：可配置起始高度
        private float takeOffHeight;
        private float landingHeight;
        private float smoothTime;

        // 运行时
        private float velocity;            // SmoothDamp 用
        private bool isTakingOff = true;  // 起飞阶段
        private bool isLanding;           // 降落阶段

        private float totalDuration;
        private float startTime;

        public override void Init()
        {
            base.Init();

            /*---------- 读取配置 ----------*/
            startHeight = BuffData.Data.GetFloat("Start", 0f);
            takeOffHeight = BuffData.Data.GetFloat("Fly", 1f);
            landingHeight = BuffData.Data.GetFloat("Land", 0f);
            smoothTime = BuffData.Data.GetFloat("Time", 0.1f);

            totalDuration = Skill.SkillData.BuffLastTime ?? BuffData.LastTime;
            startTime = Time.time;

            /*---------- 可选：立即设成配置的起始高度，避免扰动 ----------*/
            Unit.Height = startHeight;

            isTakingOff = true;
            isLanding = false;
            //velocity = (takeOffHeight - landingHeight) / smoothTime;
            velocity = 0f;
        }

        public override void Apply()
        {
            base.Apply();

            if (isTakingOff)
            {
                Unit.Height = Mathf.SmoothDamp(Unit.Height, takeOffHeight, ref velocity, smoothTime);
                //Log.Debug(Unit.Height);
                if (Mathf.Abs(Unit.Height - takeOffHeight) < 0.01f)
                {
                    Unit.Height = takeOffHeight;
                    
                    isTakingOff = false;
                }
            }
            else if (isLanding)
            {
                Unit.Height = Mathf.SmoothDamp(Unit.Height, landingHeight, ref velocity, smoothTime);
                //Log.Debug(Unit.Height);
                if (Mathf.Abs(Unit.Height - landingHeight) < 0.01f)
                {
                    Unit.Height = landingHeight;
                    
                    isLanding = false;
                    Dead = true;
                }
            }
        }

        public override void Update()
        {
            base.Update();

            if (!isTakingOff && !isLanding && Duration.value <= smoothTime)
            {
                isLanding = true;
                velocity = 0f;
                //velocity = (takeOffHeight - landingHeight) / smoothTime;
                Apply();
            }
        }

        public override void Finish()
        {
            base.Finish();
            //if (isTakingOff || !isLanding)
                Unit.Height = landingHeight;
            Dead = true;
        }
    }
}