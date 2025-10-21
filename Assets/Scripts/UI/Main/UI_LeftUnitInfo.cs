using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FairyGUI;
using UnityEngine;

namespace MainUI
{
    partial class UI_LeftUnitInfo
    {
        GObjectPool pool;
        public Card Card;

        partial void Init()
        {
            pool = new GObjectPool(container.cachedTransform);
        }
        public void SetCard(Card card,int skillIndex)
        {
            this.Card = card;
            if (card == null)
            {
                m_empty.selectedIndex = 1;
            }
            else
            {
                m_empty.selectedIndex = 0;
                m_name.text = card.UnitData.Name;
                m_lv.text = card.Level.ToString();
                m_reset.text = (card.UnitData.ResetTime + card.UnitData.ResetTimeEx).ToString();
                m_cost.text = (card.UnitData.Cost + card.UnitData.CostEx).ToString();
                m_stop.text = card.UnitData.StopCount.ToString();
                m_agi.text = card.UnitData.AttackGap.ToString();
                m_hp.text = (card.UnitData.Hp+card.UnitData.HpEx).ToString();
                m_attack.text = (card.UnitData.Attack + card.UnitData.AttackEx).ToString();
                m_def.text = (card.UnitData.Defence + card.UnitData.DefenceEx).ToString();
                m_magdefence.text = (card.UnitData.MagicDefence + card.UnitData.MagicDefenceEx).ToString();
                
                m_CircularRange.visible = false;
                m_attackArea.visible = false;
                ShowAttackRange();

                m_Skills.RemoveChildrenToPool();
                ShowSkill(skillIndex);
            }
        }

        private void ShowAttackRange()
        {
            if (Card.UnitData.Skills is null || Card.UnitData.Skills.Length == 0)
                return;
            var mainSkill = Database.Instance.Get<SkillData>(this.Card.UnitData.Skills[0]);

            if (mainSkill.AttackPoints is not null && mainSkill.AttackPoints.Length >= 0)
            {
                ShowAttackPoints(mainSkill);
            }
            else if (mainSkill.AttackRange > 0)
            {
                ShowAttackArea(mainSkill);
            }
        }
        private void ShowAttackPoints(SkillData mainSkill)
        {
            m_attackArea.visible = true;
            foreach (var item in m_attackArea.GetChildren())
            {
                pool.ReturnObject(item);
            }
            m_attackArea.RemoveChildren();

            float midX = (mainSkill.AttackPoints.Max(x => x.x) + mainSkill.AttackPoints.Min(x => x.x)) / 2f;
            float midY = (mainSkill.AttackPoints.Max(x => x.y) + mainSkill.AttackPoints.Min(x => x.y)) / 2f;
            foreach (var point in mainSkill.AttackPoints)
            {
                var a = pool.GetObject(UI_AttackArea.URL) as UI_AttackArea;
                m_attackArea.AddChild(a);
                a.xy = new Vector2((point.x - midX) * 16 - 6f, (point.y - midY) * 16 - 6f);
                a.m_type.selectedIndex = (point.x == 0 && point.y == 0) ? 1 : 0;
            }
        }
        private void ShowAttackArea(SkillData mainSkill)
        {
            m_CircularRange.visible = true;
            m_R.text = "R=" + mainSkill.AttackRange.ToString();
        }
        private void ShowSkill(int skillIndex)
        {
            if (Card.UnitData.MainSkill != null)
            {
                for (int i = 0; i < Card.UnitData.MainSkill.Length; i++)
                {
                    int skill = Card.UnitData.MainSkill[i];
                    var uiSkill = m_Skills.AddItemFromPool() as UI_SkillInfo;
                    uiSkill.SetSkill(skill);
                    uiSkill.m_selected.selectedIndex = i == skillIndex ? 1 : 0;
                }
            }
        }
    }
}
