using System;
using System.Collections;
using UnityEngine;
using System.Linq;
using System.Collections.Generic;
using FairyGUI;

namespace BattleUI
{
    partial class UI_BattleUnit
    {
        GameData gameData => GameData.Instance;
        public Unit Unit;
        //public float ScrollSpeed = 1.0f;
        //public float ScrollY = 0;
        public CountDown refreshCD = new CountDown(0.5f);
        public int BuffInfoIndex = 0;
        partial void Init()
        {
            touchable = false;
        }
        public void SetUnit(Unit unit)
        {
            this.Unit = unit;
            unit.uiUnit = this;
            m_unitType.selectedIndex = unit.UnitData.HpBarType;
            m_readyControl.selectedIndex = 0;
            m_skillCount.selectedIndex = 0;
            if (GameData.Instance.showHP)
            {
                m_eHp.m_ShowDetail.selectedIndex = 1;
            }
            Flush();
        }

        protected override void OnUpdate()
        {
            m_isPreview.selectedIndex = BattleManager.Instance.IsShowDetails ? 1 : 0;
            m_showBuffInfo.selectedIndex = ((Unit is Units.干员 || Unit is Units.敌人) && Unit.Buffs.Count > 0) ? 1 : 0;
            if (Unit != null)
            {
                Flush();
                if (m_isPreview.selectedIndex == 1)
                    ShowBuffInfo(Unit);
            }
            base.OnUpdate();
        }

        public void Flush()
        {
            if (Unit is Units.敌人 u)
            {
                if (!u.Visiable)
                {
                    m_unitType.selectedIndex = 3;
                    return;
                }
                else
                    m_unitType.selectedIndex = Unit.UnitData.HpBarType;
            }
            if (!Unit.ElementProtect.Finished())
            {
                m_elementBar.m_Recover.selectedIndex = 2;
                m_elementBar.max = Unit.ElementProtectMax;
                m_elementBar.value = Unit.ElementProtectMax - Unit.ElementProtect.value;
            }
            else
            {
                float elementValue = Unit.InjurePoint;
                m_elementBar.max = 1000;
                m_elementBar.value = 1000 - elementValue;
                m_elementBar.m_Recover.selectedIndex = elementValue == 0 ? 0 : 1;
                if (GameData.Instance.showElement && m_elementBar.value < 1000)
                {
                    m_elementBar.m_ShowDetail.selectedIndex = 1;
                }
                else
                { 
                    m_elementBar.m_ShowDetail.selectedIndex = 0;
                }
            }

            xy = Unit.UnitModel.GetModelPositon().WorldToUI();
            if (m_unitType.selectedIndex == 0 || m_unitType.selectedIndex == 2)
            {
                if (Unit.LifeTime != null)
                {
                    m_hp.max = Unit.UnitData.LifeTime;
                    m_hp.value = Unit.LifeTime.value;
                }
                else
                {
                    m_hp.max = Unit.MaxHp;
                    m_hp.value = Unit.Hp;
                }
                if (Unit.MainSkill != null)
                {
                    if (Unit.MainSkill.MaxPower > 0)
                    {
                        if (Unit.MainSkill.Opening.Finished())
                        {
                            m_sk.value = Unit.MainSkill.Power - Unit.MainSkill.MaxPower * Mathf.FloorToInt(Unit.MainSkill.Power / Unit.MainSkill.MaxPower);
                            m_sk.max = Unit.MainSkill.MaxPower;
                        }
                        else
                        {
                            m_sk.value = Unit.MainSkill.Opening.value;
                            m_sk.max = Unit.MainSkill.SkillData.OpenTime;
                        }
                        if (Unit.MainSkill.Power == Unit.MainSkill.MaxPower * Unit.MainSkill.PowerCount && Unit.MainSkill.Power != 0)
                        {
                            m_sk.value = m_sk.max;
                        }

                        if (!Unit.MainSkill.Opening.Finished())
                        {
                            m_sk.m_useControl.selectedIndex = 1;
                        }
                        else
                            m_sk.m_useControl.selectedIndex = 0;

                        if (Unit.MainSkill.Power >= Unit.MainSkill.MaxPower)
                        {
                            if (Unit.MainSkill.PowerCount == 1)
                            {
                                m_readyControl.selectedIndex = 1;
                                m_skillCount.selectedIndex = 0;
                            }
                            else
                            {
                                m_skillCount.selectedIndex = 1;
                                m_skillCount_2.text = Mathf.FloorToInt(Unit.MainSkill.Power / Unit.MainSkill.MaxPower).ToString();
                            }
                        }
                        else
                        {
                            m_readyControl.selectedIndex = Unit.MainSkill.SkillData.CanStop && !Unit.MainSkill.Opening.Finished() ? 2 : 0;
                            m_skillCount.selectedIndex = 0;
                        }
                    }
                    else
                    {
                        m_sk.value = 0;
                        m_readyControl.selectedIndex = 0;
                        m_skillCount.selectedIndex = 0;
                    }
                }
            }
            if (m_unitType.selectedIndex == 1)
            {
                if (Unit.LifeTime != null)
                {
                    m_eHp.max = Unit.UnitData.LifeTime;
                    m_eHp.value = Unit.LifeTime.value;
                }
                else
                {
                    m_eHp.max = Unit.MaxHp;
                    m_eHp.value = Unit.Hp;
                }
                m_eHp.visible = Unit.LifeTime == null ? m_eHp.value != m_eHp.max : true;
            }
            if (m_unitType.selectedIndex == 4)
            {
                m_bigHp.max = Unit.MaxHp;
                m_bigHp.value = Unit.Hp;
                m_bigHp.visible = true;
            }
            if (m_unitType.selectedIndex == 5)
            {
                m_bossHp.max = Unit.MaxHp;
                m_bossHp.value = Unit.Hp;
                m_bossHp.visible = true;
            }
            //Log.Debug(Unit.UnitData.Name + ":" + Unit.Hp);
        }
        public void ShowBuffInfo(Unit unit)
        {
            if (refreshCD.Update(SystemConfig.DeltaTime))
            {
                BuffInfoIndex++;
                BuffInfoIndex %= 5;
                refreshCD.Set(2.5f);
            }
            //else
            //    return;
            List<Buff> buffs = unit.Buffs;
            m_infoList.RemoveChildrenToPool();
            var title = m_infoList.AddItemFromPool() as UI_BuffInfo;
            title.m_name.text = "名称";
            title.m_type.text = "类型";
            title.m_dataInfo.text = "数据";
            title.m_last.text = "持续时间";
            ScrollPane scrollPane = m_infoList.scrollPane;
            for (int i = 0; i < buffs.Count && i < 5; i++)
            {
                int index = i > 5 ? (BuffInfoIndex + i) % 5 : i;
                var item = m_infoList.AddItemFromPool() as UI_BuffInfo;
                item.m_name.text = buffs[index].BuffData.Id;
                item.m_type.text = buffs[index].BuffData.Type;
                //item.m_dataInfo.text = buff.BuffData.Data;
                var data = buffs[index].BuffData.Data is not null ? string.Join("; ", buffs[index].BuffData.Data.Select(kv => $"{kv.Key}:{kv.Value}")) : "无";
                item.m_dataInfo.text = data.Replace("\n", "");
                //Debug.Log(item.m_dataInfo.text);
                Debug.Log(item.m_name.text);
                item.m_last.text = buffs[index].Duration.value.ToString("F2");
            }
            //float maxScrolly = scrollPane.viewHeight - scrollPane.contentHeight;
            //if (ScrollY + ScrollSpeed > maxScrolly)
            //{
            //    //scrollPane.posY = maxScrolly;
            //    ScrollY = maxScrolly;
            //    ScrollSpeed = -ScrollSpeed;
            //}
            //else if (ScrollY + ScrollSpeed < 0)
            //{
            //    //scrollPane.posY = 0;
            //    ScrollY = 0;
            //    ScrollSpeed = -ScrollSpeed;
            //}
            //else
            //{
            //    //scrollPane.posY = ScrollY + ScrollSpeed;
            //    scrollPane.SetPosY(ScrollY + ScrollSpeed, false);
            //    ScrollY += ScrollSpeed;
            //}
            //Debug.Log(maxScrolly);
            //Debug.Log(scrollPane.posY + "=" + ScrollY + " + " + ScrollSpeed);
        }
        // 辅助方法：将值转换为字符串（处理数组/集合）
    }
}