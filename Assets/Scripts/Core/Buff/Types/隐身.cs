using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Buffs
{
    public class 隐身 : Buff
    {
        
        CountDown rehide = new CountDown();
        float rehideTime;
        bool ignoreStoped;
        bool isStoped;
        string[] rehideTimeName = new string[] { "HideTime", "显隐时间" };
        string[] ignorsStopedName = new string[] { "IgnoreStoped", "无视阻挡" };


        public override void Init()
        {
            base.Init();
            rehideTime = this.BuffData.Data.GetFloat(rehideTimeName);
            ignoreStoped = BuffData.Data.GetBool(ignorsStopedName);
        }

        public override void Update()
        {
            base.Update();
            if (Unit.IfStoped() && !ignoreStoped)
            {
                isStoped = true;
                //WLastingEffect?.gameObject.SetActive(false);
                rehide.Set(rehideTime);
            }
            rehide.Update(SystemConfig.DeltaTime);
            //Log.Debug($"{Unit.UnitData.Id}隐身了");
            if (rehide.Finished() && !Unit.IfStoped())
            {
                isStoped = false;
            }
            if (isStoped) Unit.IfHide = ignoreStoped;
            else Unit.IfHide = true;
            Unit.ForceHide = Unit.ForceHide || ignoreStoped;
        }

        public override void UpdateView()
        {
            base.UpdateView();
            if (LastingEffect != null)
            {
                if (!LastingEffect.gameObject.activeSelf && Unit.IfHide)
                {
                    LastingEffect.Play();
                }
                LastingEffect?.gameObject?.SetActive(Unit.IfHide);
            }
        }
    }
}
