using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace BattleUI
{
    partial class UI_SkillUsePanel
    {
        public Unit Unit;
        public bool skillUseing = false;
        public void SetUnit(Unit unit)
        {
            this.Unit = unit;
        }
        partial void Init()
        {
            m_ShowSkillRange.onClick.Add(() =>
            {
                if (skillUseing)
                    m_ShowSkillRange.selected = true;
                else
                {
                    BattleCamera.Instance.showSkillArea = m_ShowSkillRange.selected;
                    //Debug.Log("showSkillArea:" + BattleCamera.Instance.showSkillArea);
                    BattleCamera.Instance.ShowUnitAttackArea();
                }
            });
        }

        protected override void OnUpdate()
        {
            base.OnUpdate();
            if (Unit == null) return;
            var s = Unit.MainSkill;
            if (s != null)
            {
                m_mainSkillInfo.visible = true;
                m_ShowSkillRange.visible = true;
                m_mainSkillInfo.m_using.selectedIndex = s.Opening.Finished() ? 0 : 1;
                m_mainSkillInfo.m_canStop.selectedIndex = s.SkillData.CanStop ? 1 : 0;
                m_mainSkillInfo.m_isReady.selectedIndex = m_mainSkillInfo.max == m_mainSkillInfo.value ? 1 : 0;
                m_mainSkillInfo.m_icon.url = s.SkillData.Icon.ToSkillIcon();
                //Log.Debug(m_mainSkillInfo.m_icon.url);
                if (!s.Opening.Finished())
                {
                    m_mainSkillInfo.max = s.SkillData.OpenTime;
                    m_mainSkillInfo.value = s.Opening.value;
                    m_ShowSkillRange.selected = true;
                    skillUseing = true;
                }
                else
                {
                    m_mainSkillInfo.max = Unit.MainSkill.MaxPower;
                    m_mainSkillInfo.value = Unit.MainSkill.Power - Unit.MainSkill.MaxPower * Mathf.FloorToInt(Unit.MainSkill.Power / Unit.MainSkill.MaxPower);
                    skillUseing = false;
                }
                if (Unit.MainSkill.Power == Unit.MainSkill.MaxPower * Unit.MainSkill.PowerCount)
                {
                    m_mainSkillInfo.value = m_mainSkillInfo.max;
                }
                //m_mainSkillInfo.m_text.text = $"{(int)m_mainSkillInfo.value}/{ (int)m_mainSkillInfo.max}";
            }
            else
            {
                m_mainSkillInfo.visible = false;
                m_ShowSkillRange.visible = false;
            }
        }
    }
}
