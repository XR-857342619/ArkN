using FairyGUI;
using MainUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace BattleUI
{
    partial class UI_BattleLeft
    {
        Texture alt;
        Unit Unit;
        GObjectPool pool;
        GLabel atk;
        GLabel def;
        GLabel agi;
        GLabel magDef;
        GLabel block;
        GLabel cost;
        GLabel resetTime;
        GLabel atkGap;
        GLabel hpRecover;
        GLabel hatred;
        GLabel weight;
        GLabel speed;
        GLabel test;
        partial void Init()
        {
            FairyGUI.UIConfig.allowSoftnessOnTopOrLeftSide = false;
            alt = ResHelper.GetAsset<Texture>(PathHelper.OtherPath + "a遮罩");
            pool = new GObjectPool(container.cachedTransform);
            //m_standPic.m_standPic.tex
            atk = AddLabel("攻击");
            def = AddLabel("防御");
            agi = AddLabel("攻速");
            magDef = AddLabel("法抗");
            block = AddLabel("阻挡");
            cost = AddLabel("费用");
            resetTime = AddLabel("再部署时间");
            atkGap = AddLabel("攻击间隔");
            hpRecover = AddLabel("生命恢复");
            hatred = AddLabel("嘲讽等级");
            weight = AddLabel("重量");
            speed = AddLabel("速度");
            test = AddLabel("未来可能有的一堆属性显示");
        }

        protected override void OnUpdate()
        {
            base.OnUpdate();
            if (Unit != null)
            {
                m_subSkill.text = Unit.UnitData.AblitityInfo;
                m_name.text = Unit.UnitData.Name;
                atk.GetChild("value").asTextField.text = Unit.Attack.ToString();
                def.GetChild("value").asTextField.text = Unit.Defence.ToString();
                agi.GetChild("value").asTextField.text = Unit.Agi.ToString();
                magDef.GetChild("value").asTextField.text = Unit.MagicDefence.ToString();
                block.GetChild("value").asTextField.text = Unit.StopCount.ToString();
                cost.GetChild("value").asTextField.text = Unit is Units.干员 uc ? uc.Cost.ToString() : Unit.UnitData.Cost.ToString();
                resetTime.GetChild("value").asTextField.text = Unit is Units.干员 ur ? ur.ResetTime.ToString() : Unit.UnitData.ResetTime.ToString();
                atkGap.GetChild("value").asTextField.text = Unit.AttackGap.ToString();
                hpRecover.GetChild("value").asTextField.text = Unit.HpRecover.ToString();
                hatred.GetChild("value").asTextField.text = Unit.Hatre.ToString();
                weight.GetChild("value").asTextField.text = Unit.Weight.ToString();
                speed.GetChild("value").asTextField.text = Unit.Speed.ToString();
                test.GetChild("value").asTextField.text = "114514 1919810";
                //m_atk.text = Unit.Attack.ToString();
                //m_def.text = Unit.Defence.ToString();
                //m_agi.text = Unit.Agi.ToString();
                //m_magDef.text = Unit.MagicDefence.ToString();
                //m_block.text = Unit.UnitData.StopCount.ToString();
                m_Hp.max = Unit.MaxHp;
                m_Hp.value = Unit.Hp;
                var t = ResHelper.GetAsset<Texture>(PathHelper.StandPicPath + Unit.UnitData.StandPic);
                if (t != null)
                    m_standPic.m_standPic.texture = new FairyGUI.NTexture(t, alt, 1, 1);
                else
                    m_standPic.m_standPic.icon = "";
                if (Unit is Units.干员)
                {
                    m_Lv.text = Unit.UnitData.Level.ToString();
                    m_palyerUnit.selectedIndex = 0;
                    (m_Pro as MainUI.UI_Pro).m_p.selectedIndex = (int)Unit.UnitData.Profession;
                    var mainSkill = Unit.MainSkill;
                    if (mainSkill != null)
                    {                     
                        m_SkillName.text = mainSkill.SkillData.Name;

                        m_skillIcon.icon = mainSkill.SkillData.Icon.ToSkillIcon();
                        (m_Recover as MainUI.UI_Recover).m_recover.selectedIndex = (int)mainSkill.SkillData.PowerType;
                        (m_UseType as MainUI.UI_UseType).m_useType.selectedIndex = (int)mainSkill.SkillData.UseType;
                        m_lastTime.text = mainSkill.SkillData.OpenTime > 1000 ? "∞" : mainSkill.SkillData.OpenTime.ToString();
                        m_time.selectedIndex = mainSkill.SkillData.OpenTime >= 1f ? 0 : 1;
                        m_SkillDesc.text = mainSkill.SkillData.Desc;
                    }
                    else
                    {
                        m_palyerUnit.selectedIndex = 1;
                        m_midUnitDesc.text = "";
                    }
                }
                else
                {
                    m_palyerUnit.selectedIndex = 1;
                    m_midUnitDesc.text = Unit.UnitData.AblitityInfo;
                }
            }
        }

        public void SetUnit(Unit unit)
        {
            this.Unit = unit;

            foreach (var item in m_attackArea.GetChildren())
            {
                pool.ReturnObject(item);
            }
            m_attackArea.RemoveChildren();

            if (unit != null && unit is Units.干员)
            {
                var mainSkill = Unit.FirstSkill;
                if (mainSkill.SkillData.AttackPoints == null) return;
                int sX = (mainSkill.SkillData.AttackPoints.Max(x => x.x) - mainSkill.SkillData.AttackPoints.Min(x => x.x)) + 1;
                int sY = (mainSkill.SkillData.AttackPoints.Max(x => x.y) - mainSkill.SkillData.AttackPoints.Min(x => x.y)) + 1;
                int reX = sX - mainSkill.SkillData.AttackPoints.Max(x => x.x) - 1;
                int reY = sY - mainSkill.SkillData.AttackPoints.Max(x => x.y) - 1;
                int sieze = sX*sY;
                m_attackArea.lineCount = sY;
                m_attackArea.columnCount = sX;
                for (int i = 0; i < sieze; i++)
                {
                    var a = m_attackArea.AddItemFromPool() as UI_AttackArea;
                    a.m_type.selectedIndex = 2;
                }
                foreach (var point in mainSkill.SkillData.AttackPoints)
                {
                    //var a = pool.GetObject(UI_AttackArea.URL) as UI_AttackArea;
                    //m_attackArea.AddChild(a);
                    //a.xy = new Vector2((point.x - midX) * 16-6f, (point.y - midY) * 16-6f);
                    int index = point.x + reX + sX * (point.y + reY);
                    //Debug.Log("sX:" + sX + " sY:" + sY + " reX:" + reX + " reY:" + reY);
                    //Debug.Log(point.x + "+" + reX + "+" + sX + "*(" + point.y + "+" + reY + ")=" + index);
                    var a = m_attackArea.GetChildAt(index) as UI_AttackArea;
                    a.m_type.selectedIndex = (point.x == 0 && point.y == 0) ? 1 : 0;
                }
            }
        }
        public GLabel AddLabel(string title)
        {
            GLabel label = m_attrList.AddItemFromPool().asLabel;
            label.text = title;
            //label.GetChild("value").asTextField.text = text;
            //Log.Debug(title + ":" + text);
            return label;
        }
    }
}
