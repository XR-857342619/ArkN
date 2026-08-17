using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace BattleUI
{
    partial class UI_BuildSprite
    {
        public Units.干员 Unit;
        public void SetUnit(Units.干员 unit)
        {
            this.Unit = unit;
            m_typeControl.selectedIndex = (int)unit.UnitData.Profession;
            m_cost.text = unit.GetCost().ToString();
            m_bar.max = unit.ResetTime;
            IconHelper.SetTexture(m_headIcon.m_headIcon, Unit.UnitData.HeadIcon, IconType.HeadIcon);
        }
        //partial void Init()
        //{
        //    //base.Init();
        //    Debug.Log(Unit?.UnitData?.HeadIcon);
        //    if (Unit is null || Unit.UnitData is null || Unit.UnitData.HeadIcon is null) return;
        //    IconHelper.SetTexture(m_headIcon.m_headIcon, Unit.UnitData.HeadIcon, IconType.HeadIcon);
        //    Debug.Log("SetTexture");
        //}
        protected override void OnUpdate()
        {
            base.OnUpdate();
            //this.width = Screen.width/13 <= 50 ? 50 : Screen.width/13;
            if (Unit == null) return;
            bool cooling = !Unit.Reseting.Finished() && !BattleManager.Instance.IsNoCD;
            m_cooldown.selectedIndex = cooling ? 1 : 0;

            if (float.IsInfinity(Unit.Reseting.value))
            {
                m_cooldown.selectedIndex = 0;
                //m_canUse.selectedIndex = 0;
            }
            else
            {
                m_bar.value = Unit.Reseting.value;
                m_resetTime.text = Unit.Reseting.value.ToString("F1");
            }
            m_canUse.selectedIndex = Unit.Useable() ? 0 : 1;
            //m_headIcon.m_headIcon.icon = Unit.UnitData.HeadIcon.ToHeadIcon();
        }
    }
}
