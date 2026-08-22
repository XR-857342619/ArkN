using FairyGUI;
using MainUI;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Units;
using UnityEngine;
using static UnityEngine.UI.CanvasScaler;

namespace BattleUI
{
    partial class UI_BattleUnit
    {
        GameData gameData => GameData.Instance;
        public Unit Unit;
        public CountDown refreshCD = new CountDown(0.5f);
        public int BuffInfoIndex = 0;

        public GProgressBar hpBar;
        public GProgressBar spBar;

        private readonly List<ProgressBarBindingView> progressBarViews = new List<ProgressBarBindingView>();

        private sealed class ProgressBarBindingView
        {
            public ProgressBarBinding binding;
            public UI_ProgressSet item;
            public GProgressBar bar;
        }

        partial void Init()
        {
            touchable = false;
        }

        public void SetUnit(Unit unit)
        {
            this.Unit = unit;
            unit.uiUnit = this;


            ApplyHpBar(unit.UnitData.HpBarType, GameData.Instance.showHP, ref hpBar);
            ApplySpBar(unit.UnitData.SpBarType, false, ref spBar);

            m_readyControl.selectedIndex = 0;
            m_skillCount.selectedIndex = 0;

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
            if (Unit == null) return;

            if (Unit is Units.敌人 enemy && !enemy.Visiable)
            {
                m_hpType.selectedPage = "无";
                return;
            }

            xy = Unit.UnitModel.GetModelPositon().WorldToUI();

            FlushElementBar();
            FlushHpBar();
            FlushSpBar();
            FlushProgressList();
        }

        private void FlushElementBar()
        {
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
                m_elementBar.m_ShowDetail.selectedIndex =
                    (GameData.Instance.showElement && m_elementBar.value < 1000) ? 1 : 0;
            }
        }

        private void FlushHpBar()
        {
            string hpType = Unit.UnitData.HpBarType;
            ApplyHpBar(hpType, GameData.Instance.showHP, ref hpBar);

            if (IsNoneBar(hpType)) return;

            UpdateProgress(hpBar, Unit.LifeTime, Unit.MaxHp, Unit.Hp);

            // 使用小型血条的敌人类单位，满血时隐藏血条。
            if (IsSmallBar(hpType) && Unit is Units.敌人)
            {
                hpBar.visible = Unit.LifeTime == null ? hpBar.value != hpBar.max : true;
            }
        }

        private void FlushSpBar()
        {
            string spType = Unit.UnitData.SpBarType;

            // 与旧逻辑保持一致：血条和技力条都不显示时，不刷新技力/就绪状态。
            if (IsNoneBar(Unit.UnitData.HpBarType) && IsNoneBar(spType)) return;

            if (Unit.MainSkill == null || Unit.MainSkill.MaxPower <= 0)
            {
                spBar.value = 0;
                m_readyControl.selectedIndex = 0;
                m_skillCount.selectedIndex = 0;
                ApplySpBar(spType, false, ref spBar);
                return;
            }

            bool isUsing = !Unit.MainSkill.Opening.Finished();

            if (isUsing)
            {
                spBar.value = Unit.MainSkill.Opening.value;
                spBar.max = Unit.MainSkill.SkillData.OpenTime;
            }
            else
            {
                float power = Unit.MainSkill.Power;
                float maxPower = Unit.MainSkill.MaxPower;
                spBar.value = power - maxPower * Mathf.FloorToInt(power / maxPower);
                spBar.max = maxPower;
            }

            if (Unit.MainSkill.Power == Unit.MainSkill.MaxPower * Unit.MainSkill.PowerCount &&
                Unit.MainSkill.Power != 0)
            {
                spBar.value = spBar.max;
            }

            ApplySpBar(spType, isUsing, ref spBar);

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
        /// <summary>
        /// 从 UnitProgressBarManager 读取绑定信息，并刷新 m_progressList。
        /// 绑定关系由 Skill/Buff 基类注册，这里只负责 UI 显示。
        /// </summary>
        private void FlushProgressList()
        {
            IReadOnlyList<ProgressBarBinding> bindings = UnitProgressBarManager.Instance.GetBindings(Unit);

            if (bindings == null || bindings.Count == 0)
            {
                if (m_progressList.numItems != 0)
                    m_progressList.RemoveChildrenToPool();

                progressBarViews.Clear();
                return;
            }

            if (progressBarViews.Count != bindings.Count)
            {
                RebuildProgressBarViews(bindings);
            }
            else
            {
                for (int i = 0; i < progressBarViews.Count; i++)
                    progressBarViews[i].binding = bindings[i];
            }

            UpdateProgressBarViews();
        }

        private void RebuildProgressBarViews(IReadOnlyList<ProgressBarBinding> bindings)
        {
            m_progressList.RemoveChildrenToPool();
            progressBarViews.Clear();

            for (int i = 0; i < bindings.Count; i++)
            {
                ProgressBarBinding binding = bindings[i];
                UI_ProgressSet item = m_progressList.AddItemFromPool() as UI_ProgressSet;
                GProgressBar bar = null;

                if (item != null)
                {
                    item.m_BarType.selectedPage = binding.BarType;
                    bar = GetVisibleProgressBar(item);
                }

                progressBarViews.Add(new ProgressBarBindingView
                {
                    binding = binding,
                    item = item,
                    bar = bar,
                });
            }
        }

        private void UpdateProgressBarViews()
        {
            for (int i = 0; i < progressBarViews.Count; i++)
            {
                ProgressBarBindingView view = progressBarViews[i];
                ProgressBarBinding binding = view.binding;
                if (binding == null || view.item == null) continue;

                if (view.item.m_BarType.selectedPage != binding.BarType)
                {
                    view.item.m_BarType.selectedPage = binding.BarType;
                    view.bar = GetVisibleProgressBar(view.item);
                }

                if (view.bar == null)
                {
                    view.bar = GetVisibleProgressBar(view.item);
                    if (view.bar == null) continue;
                }

                float max;
                float value;

                if (binding.IsSkill)
                {
                    var skill = (Skill)binding.Source;
                    float maxPower = skill.MaxPower;
                    max = maxPower > 0 ? maxPower : 1f;
                    value = skill.Power - maxPower * Mathf.FloorToInt(skill.Power / maxPower);
                }
                else
                {
                    var buff = (Buff)binding.Source;
                    if (buff is MultiLevelBuff multiLevelBuff)
                    {
                        max = multiLevelBuff.MaxLevel > 0 ? multiLevelBuff.MaxLevel : 1f;
                        value = multiLevelBuff.Dead || multiLevelBuff.Level <= 0 ? 0f : multiLevelBuff.Level;
                    }
                    else
                    {
                        max = buff.Duration.value > 0 ? buff.Duration.value : 1f;
                        value = buff.Dead || buff.Duration.value <= 0 ? 0f : buff.Duration.value;
                    }
                }

                view.bar.max = max;
                view.bar.value = value;
            }
        }

        private GProgressBar GetVisibleProgressBar(UI_ProgressSet item)
        {
            for (int i = 0; i < item.numChildren; i++)
            {
                GObject child = item.GetChildAt(i);
                if (child != null && child.visible && child is GProgressBar bar)
                {
                    return bar;
                }
            }

            return null;
        }


        private void UpdateProgress(GProgressBar bar, CountDown lifeTime, float maxHp, float hp)
        {
            if (bar == null) return;

            if (lifeTime != null)
            {
                bar.max = Unit.UnitData.LifeTime;
                bar.value = lifeTime.value;
            }
            else
            {
                bar.max = maxHp;
                bar.value = hp;
            }
        }

        private static bool IsNoneBar(string type)
        {
            return string.IsNullOrEmpty(type) || type == "无";
        }

        private static bool IsSmallBar(string type)
        {
            return !string.IsNullOrEmpty(type) && type.StartsWith("小型");
        }

        private static bool IsBigBar(string type)
        {
            return !string.IsNullOrEmpty(type) && type.StartsWith("大型");
        }

        private static string GetSmallBarColor(string type, string fallback)
        {
            if (string.IsNullOrEmpty(type)) return fallback;

            int index = type.IndexOf(':');
            if (index < 0 || index >= type.Length - 1) return fallback;

            return type.Substring(index + 1);
        }

        /// <summary>
        /// 根据 HpBarType 切换 m_hpType 页面，并返回本次实际使用的进度条组件。
        /// </summary>
        public void ApplyHpBar(string type, bool showDetail, ref GProgressBar bar)
        {
            if (IsNoneBar(type)) type = "无";

            if (IsSmallBar(type))
            {
                string color = GetSmallBarColor(type, "红");

                m_hpType.selectedPage = "小型";
                m_hp.m_useControl.selectedPage = color;
                m_hp.m_ShowDetail.selectedIndex = (Unit is Units.敌人 && showDetail) ? 1 : 0;
                bar = m_hp;
                return;
            }

            switch (type)
            {
                case "大型":
                    m_hpType.selectedPage = "大型";
                    m_bigHp.m_type.selectedPage = "血量";
                    bar = m_bigHp;
                    break;
                case "Boss":
                    m_hpType.selectedPage = "Boss";
                    bar = m_bossHp;
                    break;
                case "精英":
                    m_hpType.selectedPage = "精英";
                    bar = m_bossHp;
                    break;
                default:
                    m_hpType.selectedPage = "无";
                    bar = m_hp;
                    break;
            }
        }

        /// <summary>
        /// 根据 SpBarType 切换 m_spType 页面，并返回本次实际使用的技力条组件。
        /// </summary>
        public void ApplySpBar(string type, bool isUsing, ref GProgressBar bar)
        {
            if (IsNoneBar(type)) type = "无";

            if (IsSmallBar(type))
            {
                string color = GetSmallBarColor(type, "绿");

                m_spType.selectedPage = "小型";
                m_sk.m_useControl.selectedPage = isUsing ? "使用" : color;
                m_sk.m_ShowDetail.selectedIndex = 0;
                bar = m_sk;
                return;
            }

            if (IsBigBar(type))
            {
                m_spType.selectedPage = "大型";
                m_bigSp.m_type.selectedPage = isUsing ? "使用" : "技力";
                bar = m_bigSp;
                return;
            }

            m_spType.selectedPage = "无";
            bar = m_sk;
        }

        public void ShowBuffInfo(Unit unit)
        {
            if (refreshCD.Update(SystemConfig.DeltaTime))
            {
                BuffInfoIndex++;
                BuffInfoIndex %= 5;
                refreshCD.Set(2.5f);
            }

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
                var data = buffs[index].BuffData.Data is not null
                    ? string.Join("; ", buffs[index].BuffData.Data.Select(kv => $"{kv.Key}:{kv.Value}"))
                    : "无";
                item.m_dataInfo.text = data.Replace("\n", "");
                item.m_last.text = buffs[index].Duration.value.ToString("F2");
            }
        }
    }
}