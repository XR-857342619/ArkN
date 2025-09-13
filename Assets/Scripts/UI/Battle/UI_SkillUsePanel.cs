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
        public List<Unit> tileUnits;
        public bool skillUseing = false;
        public void SetUnit(Unit unit, List<Unit> tileUnits)
        {
            this.Unit = unit;
            this.tileUnits = tileUnits;
            //Debug.Log("tileUnits:" + tileUnits.Count);
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
            m_headIconLast.onClick.Add(() =>
            {
                Debug.Log("lastOp");
                if (tileUnits == null || tileUnits.Count == 1) return;
                if (Unit == tileUnits.First()) return;
                Unit = tileUnits[tileUnits.IndexOf(Unit) - 1];
                UI_Battle.Instance.m_left.SetUnit(Unit);
                UI_Battle.Instance.selectedUnit = Unit;
                BattleCamera.Instance.ShowUnitInfo(Unit);
            });
            m_headIconNext.onClick.Add(() =>
            {
                Debug.Log("nextOp");
                if (tileUnits == null || tileUnits.Count == 1) return;                
                if (Unit == tileUnits.Last()) return;
                Unit = tileUnits[tileUnits.IndexOf(Unit) + 1];
                UI_Battle.Instance.m_left.SetUnit(Unit);
                UI_Battle.Instance.selectedUnit = Unit;
                BattleCamera.Instance.ShowUnitInfo(Unit);
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
            if (tileUnits == null || tileUnits.Count == 1)
            {
                m_LastOp.visible = false;
                m_NextOp.visible = false;
            }
            else if (tileUnits.Count > 1 && Unit == tileUnits.First())
            {
                m_LastOp.visible = false;
                m_NextOp.visible = true;
                m_headIconNext.url = IconHelper.ToHeadIcon(tileUnits[tileUnits.IndexOf(Unit) + 1].UnitData.HeadIcon);
                //Debug.Log("headIconNext:" + m_headIconNext.icon);
            }
            else if (tileUnits.Count > 1 && Unit == tileUnits.Last())
            {
                m_LastOp.visible = true;
                m_NextOp.visible = false;
                m_headIconLast.url = IconHelper.ToHeadIcon(tileUnits[tileUnits.IndexOf(Unit) - 1].UnitData.HeadIcon);
                //Debug.Log("headIconLast:" + m_headIconLast.icon);
            }
            else
            {
                m_LastOp.visible = true;
                m_NextOp.visible = true;
                m_headIconLast.url = IconHelper.ToHeadIcon(tileUnits[tileUnits.IndexOf(Unit) - 1].UnitData.HeadIcon);
                m_headIconNext.url = IconHelper.ToHeadIcon(tileUnits[tileUnits.IndexOf(Unit) + 1].UnitData.HeadIcon);
                //Debug.Log("headIconLast:" + m_headIconLast.icon + " headIconNext:" + m_headIconNext.icon);
            }
        }
    }
}
