using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace MainUI
{
    partial class UI_HalfUnit
    {
        public Card Card;
        public void SetCard(Card card,int skillIndex)
        {
            this.Card = card;
            m_typeControl.selectedIndex = (int)card.UnitData.Profession;
            m_halfPic.icon = "ui://Res/" + card.UnitData.HalfIcon;
            //if (card.UnitData.MainSkill != null)
            //{
            //    Debug.Log(card.UnitData.MainSkill[skillIndex]);
            //    Debug.Log(Database.Instance.Get<SkillData>(card.UnitData.MainSkill[skillIndex]).Icon);
            //}
            //else
            //    Debug.Log("main skill null");
            //m_skillIcon.icon = card.UnitData.MainSkill == null ? "" : Database.Instance.Get<SkillData>(card.UnitData.MainSkill[skillIndex]).Icon.ToSkillIcon();
            IconHelper.SetTexture(m_skillIcon, card.UnitData.MainSkill == null ? "" : Database.Instance.Get<SkillData>(card.UnitData.MainSkill[skillIndex]).Icon, IconType.SkillIcon);
            m_star.selectedIndex = card.UnitData.Rare;
            m_ugrade.selectedIndex = card.UnitData.Upgrade;
            //m_stars.RemoveChildrenToPool();
            //for (int i = 0; i < card.UnitData.Rare; i++)
            //{
            //    m_stars.AddItemFromPool();
            //}
            m_lv.text = card.Level.ToString();
            m_name.text = card.UnitData.Name.ToString();
        }
    }
}
