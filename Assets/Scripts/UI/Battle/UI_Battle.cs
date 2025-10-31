using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FairyGUI;
using UnityEngine;

namespace BattleUI
{
    partial class UI_Battle:IGameUIView
    {
        public static UI_Battle Instance;
        public Battle Battle;

        GObjectPool UIPool;

        public Unit selectedUnit;
        public Unit mvp;
        public List<Unit> units;
        public Units.干员 SelectPlayerUnit => selectedUnit as Units.干员;

        GameObject worldUI;
        UI_DragPanel dragPanel;
        
        partial void Init()
        {
            Instance = this;
            UIPool = new GObjectPool(container.cachedTransform);
            m_state.onChanged.Add(pageChange);
            m_SkillUseBack.onClick.Add(StopChooseUnit);
            m_SkillUsePanel.m_Leave.onClick.Add(leaveUnit);
            m_SkillUsePanel.m_mainSkillInfo.onClick.Add(useMainSkill);
            m_endClick.onClick.Add(ExitBattle);
            m_Setting.onClick.Add(TryGiveup);
            m_CancelGiveUp.onClick.Add(cancelGiveup);
            m_GiveUpBack.onClick.Add(cancelGiveup);
            m_GiveUp.onClick.Add(doGiveUp);
            //m_GiveUp2.onClick.Add(doGiveUp);
            m_GameSpeed.onClick.Add(() =>
            {
                if (m_GameSpeed.m_Speed.selectedIndex != 3)
                    TimeHelper.Instance.SetFastSpeed(!TimeHelper.Instance.FastSpeed);
                else
                {
                    m_Pause.GetController("Speed").selectedIndex = 1;
                    TimeHelper.Instance.SetPause(true);
                    //TimeHelper.Instance.SetGameSpeed(m_gameSpeed.value);
                    Battle.Update();
                    //RefrashPreviewSetting();
                }
            });
            m_Pause.onClick.Add(() => 
            {
                TimeHelper.Instance.SetPause(!TimeHelper.Instance.Pause);
            });
            worldUI = ResHelper.Instantiate("Assets/Bundles/Other/UIPanel");
            GameObject.DontDestroyOnLoad(worldUI);
            dragPanel = worldUI.GetComponent<UIPanel>().ui as UI_DragPanel;
            //dragPanel.AddRelation(GRoot.inst, RelationType.Size);
            dragPanel.SetSize(GRoot.inst.size.x, GRoot.inst.size.y);
            dragPanel.visible = false;
            dragPanel.displayObject.cachedTransform.localPosition = new Vector3(-dragPanel.size.x / 2, dragPanel.size.y / 2, -50);
            dragPanel.Parent = this;
            //m_DirectionPanel
            m_gameSpeed.value = BattleManager.Instance.TimeScale = 1;
            m_skillPowerSpeed.value = BattleManager.Instance.RecoverPowervSpeed = 1;
            m_skillPowerSpeed.GetChild("title_2").asTextField.text = "1";
            m_isInfCost.selected = BattleManager.Instance.IsInfCost;
            m_isInfHealth.selected = BattleManager.Instance.IsInfHealth;
            m_isInfUnitCount.selected = BattleManager.Instance.IsInfUnitCount;
            m_isNoCD.selected = BattleManager.Instance.IsNoCD;
            m_isNoLimitBuild.selected = BattleManager.Instance.IsNoLimitBuild;
            m_isShowDetails.selected = BattleManager.Instance.IsShowDetails;

            m_gameSpeed.onChanged.Add(() =>
            {
                BattleManager.Instance.TimeScale = (int)m_gameSpeed.value;
            });
            m_skillPowerSpeed.onChanged.Add(() =>
            {
                //Debug.Log("m_skillPowerSpeed.value:" + m_skillPowerSpeed.value);
                BattleManager.Instance.RecoverPowervSpeed = (int)m_skillPowerSpeed.value;
                if (m_skillPowerSpeed.value == 16)
                    m_skillPowerSpeed.GetChild("title_2").asTextField.text = "∞";
                else
                    //Debug.Log("m_skillPowerSpeed.value:" + m_skillPowerSpeed.value);
                    m_skillPowerSpeed.GetChild("title_2").asTextField.text = m_skillPowerSpeed.value.ToString();
            });
            m_GiveUp2.onClick.Add(doGiveUp);
            m_isInfCost.onChanged.Add(() =>
            {
                BattleManager.Instance.IsInfCost = m_isInfCost.selected;
                //Debug.Log(BattleManager.Instance.IsInfCost);
            });
            m_isInfHealth.onChanged.Add(() =>
            {
                BattleManager.Instance.IsInfHealth = m_isInfHealth.selected;
            });
            m_isInfUnitCount.onChanged.Add(() =>
            {
                BattleManager.Instance.IsInfUnitCount = m_isInfUnitCount.selected;
            });
            m_isNoCD.onChanged.Add(() =>
            {
                BattleManager.Instance.IsNoCD = m_isNoCD.selected;
            });
            m_isNoLimitBuild.onChanged.Add(() =>
            {
                BattleManager.Instance.IsNoLimitBuild = m_isNoLimitBuild.selected;
            });
            m_isShowDetails.onChanged.Add(() =>
            {
                BattleManager.Instance.IsShowDetails = m_isShowDetails.selected;
            });
            //BattleManager.Instance.OpDamageInfos.Clear();

        }

        protected override void OnUpdate()
        {
            base.OnUpdate();
            if (Battle != null)
            {
                if (Input.GetKeyDown(KeyCode.Space) && !Battle.Finish)
                {
                    m_Pause.onClick.Call();
                }
                m_GameSpeed.m_Speed.selectedIndex = TimeHelper.Instance.TimeScale < 1 ? 3 : TimeHelper.Instance.FastSpeed ? 1 : 0;
                m_Pause.m_Speed.selectedIndex = TimeHelper.Instance.Pause ? 1 : 0;
                m_enemy.text = Battle.EnemyCount.ToString();
                m_hp.text = BattleManager.Instance.IsInfHealth ? "∞" : Battle.Hp.ToString();
                if (BattleManager.Instance.IsInfCost)
                    m_cost.text = "∞";
                else
                    m_cost.text = Battle.Cost.ToString();
                m_costBar.value = 1 - Battle.CostCounting.value;
                m_number.text = BattleManager.Instance.IsInfUnitCount ? "∞" : Battle.BuildCount.ToString();

                if (m_state.selectedIndex == 4)
                {
                    Vector2 pos = Camera.main.WorldToScreenPoint(BattleCamera.Instance.FocusUnit.UnitModel.GetModelPositon());
                    pos.y = Screen.height - pos.y;
                    m_SkillUsePanel.position = pos.ScreenToUI();
                }
            }
        }

        public void SetBattle(Battle battle)
        {
            this.Battle = battle;
            m_DamageInfo.RemoveChildren(0, m_DamageInfo.numChildren, true);
            UpdateUnitsLayout();
        }

        public void CreateUIUnit(Unit unit)
        {
            var battleUnit = UIPool.GetObject(UI_BattleUnit.URL) as UI_BattleUnit;
            battleUnit.SetUnit(unit);
            m_Units.AddChild(battleUnit);
        }

        public void ReturnUIUnit(Unit unit)
        {
            if (unit.uiUnit == null) return;
            unit.uiUnit.Unit = null;
            m_Units.RemoveChild(unit.uiUnit);
            UIPool.ReturnObject(unit.uiUnit);
            unit.uiUnit = null;
        }

        public void ChooseUnit(List<Unit> units)
        {
            units.Sort((a, b) => b.UnitData.NotUseTile.CompareTo(a.UnitData.NotUseTile));
            units.Sort((a, b) => b.InputTime.CompareTo(a.InputTime));
            TimeHelper.Instance.SetGameSpeed(0.2f);
            selectedUnit = units.First();
            this.units = units;
            m_state.selectedIndex = 4;
            //Debug.Log("sate:4");
            m_left.SetUnit(units.First());
            BattleCamera.Instance.ShowUnitInfo(units.First());
        }
        public void ChooseUnit(Unit unit)
        {
            TimeHelper.Instance.SetGameSpeed(0.2f);
            selectedUnit = unit;
            this.units = new List<Unit>() { unit };
            m_state.selectedIndex = 4;
            //Debug.Log("sate:4");
            m_left.SetUnit(unit);
            BattleCamera.Instance.ShowUnitInfo(unit);
        }

        public void StopChooseUnit()
        {
            if (m_state.selectedIndex == 4)
            {
                m_state.selectedIndex = 0;
                //Debug.Log("sate:0");
            }
        }

        public void BattleEnd()
        {
            freshDamageInfo();
            foreach (var uiUnit in m_Units.GetChildren())
            {
                UIPool.ReturnObject(uiUnit);
            }
            m_Units.RemoveChildren();
            foreach (var build in m_Builds.GetChildren())
            {
                //UIPool.ReturnObject(build);
                build.Dispose();
            }
            foreach (var damageInfo in m_DamageInfo.GetChildren())
            {
                //UIPool.ReturnObject(damageInfo);
                damageInfo.Dispose();
            }
            m_state.selectedIndex = 5;
            //Debug.Log("sate:5");
            BattleCamera.Instance.Blur = true;
            if (Battle.PlayerUnits.Count > 0)
            {
                var unit = Battle.PlayerUnits[UnityEngine.Random.Range(0, Battle.PlayerUnits.Count)];
                string picName = unit.UnitData.StandPic;
                //m_endPic.texture = new NTexture(ResHelper.GetAsset<Texture>(PathHelper.StandPicPath + picName));
                if (mvp != null)
                    m_endPic.icon = "ui://Res/" + mvp.UnitData.HalfIcon;
            }
            if (Battle.Win)
            {
                m_win.selectedIndex = 0;
            }
            else
            {
                m_win.selectedIndex = 1;
            }
            if (Battle.Hp == Battle.MapData.InitHp)
            {
                m_w3.visible = true;
                m_w2.visible = true;
                m_w1.visible = true;
            }
            else if (Battle.Hp >= 2)
            {
                m_w3.visible = false;
                m_w2.visible = true;
                m_w1.visible = true;
            }
            else
            {
                m_w3.visible = false;
                m_w2.visible = false;
                m_w1.visible = true;
            }
            BattleManager.Instance.OpDamageInfos.Clear();
        }

        public void ExitBattle()
        {
            BattleManager.Instance.FinishBattle();
        }

        public void UpdateUnitsLayout()
        {
            for (int i = 0; i < m_UnitList.numChildren; i++)
            {
                var head = m_UnitList.GetChildAt(i) as UI_BuildSprite;
                head.Unit = null;
                UIPool.ReturnObject(head);
            }
            //m_Builds.RemoveChildren();

            m_UnitList.RemoveChildren();

            var units = Battle.PlayerUnits.Where(x => x.InputTime == -1).GroupBy(x => x.Id).ToList();
            units.Sort((x, y) => -(y.FirstOrDefault().UnitData.Cost - x.FirstOrDefault().UnitData.Cost));
            //int width = 182;
            //Debug.Log(this.width+"--"+Screen.width);
            //if (units.Count() * width > this.width)
            //    width = (int)this.width/units.Count()+1;
            //莫名其妙会吞一个单位
            UI_BuildSprite tmp = UIPool.GetObject(UI_BuildSprite.URL) as UI_BuildSprite;
            tmp.GetController("isTmp").selectedIndex = 1;
            tmp.touchable = false;
            m_UnitList.AddChild(tmp);
            
            foreach (var group in units)
            {
                //Debug.Log(group.FirstOrDefault().UnitData.Name);
                var head = UIPool.GetObject(UI_BuildSprite.URL) as UI_BuildSprite;
                //head.width = width;
                head.GetController("isTmp").selectedIndex = 0;
                head.touchable = true;
                head.SetUnit(group.FirstOrDefault());
                
                m_UnitList.AddChild(head);
                //m_Builds.AddChild(head);

                //Log.Debug(head.width);
                //Log.Debug(this.width);
                //Log.Debug(units.IndexOf(group));
                //Log.Debug(this.width - ((units.IndexOf(group) + 2) * width));

//                head.xy = new UnityEngine.Vector2(
//# if UNITY_EDITOR
//                    this.width - ((units.IndexOf(group) + 2)* width),
//#else
//                    Screen.width - ((units.IndexOf(group) + 1) * width),
//#endif
//                    //Screen.width - (units.IndexOf(group)) * width,
//                    group.FirstOrDefault() == selectedUnit ? height - 50f : height);

                head.onClick.Set(() => clickUnit(group.FirstOrDefault()));
                head.draggable = true;
                head.onDragStart.Set(dragUnit);
                if (group.FirstOrDefault().UnitData.NotReturn)
                {
                    head.m_count.visible = true;
                    head.m_count.SetVar("n", group.Count().ToString()).FlushVars();
                }
                else
                {
                    head.m_count.visible = false;
                }
            }
            //Debug.Log(m_UnitList.numChildren);
        }

        void clickUnit(Units.干员 unit)
        {
            StopChooseUnit();//如果当前正在看干员详细状态，就退出来
            if (selectedUnit == unit)
            {
                selectedUnit = null;
                BattleCamera.Instance.CancelBuild();
                m_state.selectedIndex = 0;
                //Debug.Log("sate:0");
            }
            else
            {
                BattleCamera.Instance.CancelBuild();
                selectedUnit = unit;
                m_state.selectedIndex = 1;
                //Debug.Log("sate:1");
                inSelectUnit();
            }
        }

        void dragUnit(EventContext evt)
        {
            evt.PreventDefault();
            var unit = (evt.sender as UI_BuildSprite).Unit;
            if (!unit.CanBuild()) return;//不能造的时候拽不出来
            if (unit != selectedUnit) clickUnit(unit);
            //if (m_state.selectedIndex != 1 && unit != selectedUnit) return;//拽错了也不许出来
            m_state.selectedIndex = 2;
            //Debug.Log("sate:2");
            BattleCamera.Instance.BuildUnit = SelectPlayerUnit;
            BattleCamera.Instance.StartBuild();
        }




        void leaveUnit()
        {
            SelectPlayerUnit.LeaveMap(true);
            m_state.selectedIndex = 0;
            //Debug.Log("sate:0");
        }


        void useMainSkill()
        {
            var sk = selectedUnit.MainSkill;
            if (sk.CanOpen())
            {
                sk.DoOpen();
                m_state.selectedIndex = 0;
                //Debug.Log("sate:0");
            }
            else if (!sk.Opening.Finished() && sk.SkillData.CanStop)
            {
                //Debug.Log("停止技能");
                sk.UpdateOpening(float.MaxValue);
                sk.Opening.Finish();
            }
        }

        void pageChange()
        {
            if (m_state.selectedIndex != 3) dragPanel.visible = false;
            switch (m_state.selectedIndex)
            {
                case 0:
                    selectedUnit = null;
                    //BattleCamera.Instance.Rotate = false;
                    BattleCamera.Instance.ShowUnitInfo(null);
                    TimeHelper.Instance.SetGameSpeed(1 * BattleManager.Instance.TimeScale);
                    BattleCamera.Instance.HideHighLight();
                    UpdateUnitsLayout();
                    break;
                case 1:
                    inSelectUnit();
                    break;
                case 3:
                    dragPanel.visible = true;
                    worldUI.transform.position = selectedUnit.UnitModel.transform.position;
                    dragPanel.SetSize(GRoot.inst.size.x, GRoot.inst.size.y);
                    dragPanel.displayObject.cachedTransform.localPosition = new Vector3(-dragPanel.size.x / 2, dragPanel.size.y / 2, -50);
                    //Vector2 mousePos = Camera.main.WorldToScreenPoint(selectedUnit.UnitModel.transform.position); //Stage.inst.touchPosition.ScreenToUI();
                    //mousePos.y = Screen.height - mousePos.y;
                    //mousePos = mousePos.ScreenToUI();
                    //dragPanel.m_DirectionPanel.position = mousePos;
                    dragPanel.m_DirectionPanel.m_coner.selectedIndex = 0;
                    //dragPanel.m_DirectionBack.m_hole.position = mousePos;
                    dragPanel.m_DirectionPanel.m_grip.position = new Vector2(dragPanel.m_DirectionPanel.width / 2, dragPanel.m_DirectionPanel.height / 2);
                    break;
                case 4:
                    m_SkillUsePanel.SetUnit(selectedUnit, units);
                    break;
            }
        }

        void inSelectUnit()
        {
            TimeHelper.Instance.SetGameSpeed(0.2f);
            m_left.SetUnit(selectedUnit);
            //BattleCamera.Instance.Rotate = true;
            BattleCamera.Instance.BuildUnit = SelectPlayerUnit;
            BattleCamera.Instance.ShowHighLight();
            UpdateUnitsLayout();
        }

        void TryGiveup()
        {
            if (BattleManager.Instance.IsPreview)
            {
                m_state.selectedIndex = 7;
                freshDamageInfo();
            }
            else
                m_state.selectedIndex = 6;
            TimeHelper.Instance.SetPause(true);
            BattleCamera.Instance.BuildMode = false;
            if (selectedUnit != null)
            {
                selectedUnit.UnitModel.gameObject.SetActive(false);
                BattleCamera.Instance.BuildUnit = null;
                BattleCamera.Instance.HideUnitAttackArea();
            }
            BattleCamera.Instance.FocusUnit = null;
        }

        void cancelGiveup()
        {
            TimeHelper.Instance.SetPause(false);
            m_state.selectedIndex = 0;
            //Debug.Log("sate:0");
        }

        void doGiveUp()
        {
            //if (BattleManager.Instance.IsPreview)
            //    BattleManager.Instance.ReSetPreviwSetting();
            BattleEnd();
            BattleManager.Instance.Battle.GiveUp();
            TimeHelper.Instance.SetGameSpeed(1f);
            TimeHelper.Instance.SetFastSpeed(false);
            TimeHelper.Instance.SetPause(false);
            BattleManager.Instance.FinishBattle();
        }

        public void Enter()
        {
            foreach (var damageInfo in m_DamageInfo.GetChildren())
            {
                //UIPool.ReturnObject(damageInfo);
                damageInfo.Dispose();
            }
            m_DamageInfoList.RemoveChildren();
            foreach (OpDamageInfo damageInfo in BattleManager.Instance.OpDamageInfos)
            {
                GLabel damageInfoItem = UIPackage.CreateObject("BattleUI", "DamageInfoItem").asLabel;
                damageInfoItem.title = damageInfo.UnitId;
                //Debug.Log(damageInfo.UnitId);
                m_DamageInfoList.AddChild(damageInfoItem);
            }
            //Debug.Log("Enter");
            m_state.SetSelectedIndex(0);
            BattleManager.Instance.ReSetPreviwSetting();
            if (BattleManager.Instance.IsPreview)
            {
                m_isPreview.selectedIndex = 1;
                m_gameSpeed.value = BattleManager.Instance.TimeScale = 1;
                m_skillPowerSpeed.value = BattleManager.Instance.RecoverPowervSpeed = 1;
                m_skillPowerSpeed.GetChild("title_2").asTextField.text = "1";
                m_isInfCost.selected = BattleManager.Instance.IsInfCost;
                m_isInfHealth.selected = BattleManager.Instance.IsInfHealth;
                m_isInfUnitCount.selected = BattleManager.Instance.IsInfUnitCount;
                m_isNoCD.selected = BattleManager.Instance.IsNoCD;
                m_isNoLimitBuild.selected = BattleManager.Instance.IsNoLimitBuild;
            }
            else
            {
                m_isPreview.selectedIndex = 0;
            }
        }

        Queue<(int, int, Vector2)> textQueue = new Queue<(int, int, Vector2)>();       

        public void ShowDamageText(DamageInfo damage, int type,Vector2 pos)
        {
            int showDamage = Mathf.RoundToInt(Mathf.Abs(damage.FinalDamage));
            if (showDamage == 0) return;
            textQueue.Enqueue((showDamage, type, pos));
            DoShowText();
            //ShowDamageText(showDamage.ToString(), type, pos);
        }

        async void DoShowText()
        {
            if (textQueue.Count > 1) return;
            while (textQueue.Count > 0)
            {
                var target = textQueue.Peek();
                ShowDamageText(target.Item1.ToString(), target.Item2, target.Item3);
                await TimeHelper.Instance.WaitAsync(0.0f);
                textQueue.Dequeue();
            }
        }

        public void ShowDamageText(string text,int type,Vector2 pos)
        {
            var damageInfo = UIPackage.CreateObjectFromURL(UI_DamageInfo.URL) as UI_DamageInfo;
            damageInfo.m_number.SetVar("n", text).FlushVars();
            damageInfo.m_type.selectedIndex = type;
            m_DamageInfo.AddChild(damageInfo);
            damageInfo.position = pos;
            damageInfo.m_show.Play(() =>
            {
                damageInfo.Dispose();
            });
        }
        public void freshDamageInfo()
        {
            List<OpDamageInfo> sortedDamageInfos = BattleManager.Instance.OpDamageInfos.OrderByDescending(x => x.TotalDamage).ToList();
            if (sortedDamageInfos.Count == 0) return;
            float maxTotal = sortedDamageInfos[0].TotalDamage;
            mvp = Battle.PlayerUnits.Find(x => x.UnitData.Id == sortedDamageInfos[0].UnitId);
            GObject[] damageInfoItems = m_DamageInfoList.GetChildren();
            for (int i = 0; i < damageInfoItems.Length; i++)
            {
                GLabel damageInfoItem = damageInfoItems[i].asLabel;
                damageInfoItem.title = sortedDamageInfos[i].UnitId;
                damageInfoItem.GetChild("icon").asLoader.url = "ui://Res/" + Database.Instance.Get<UnitData>(sortedDamageInfos[i].UnitId).HeadIcon;
                GSlider totalSlider = damageInfoItem.GetChild("total").asSlider;
                GSlider normalSlier = damageInfoItem.GetChild("nomal").asSlider;
                GSlider magicSlier = damageInfoItem.GetChild("magic").asSlider;
                GSlider realSlier = damageInfoItem.GetChild("real").asSlider;
                totalSlider.max = maxTotal;
                totalSlider.value = sortedDamageInfos[i].TotalDamage;
                normalSlier.max = maxTotal;
                normalSlier.value = sortedDamageInfos[i].NomalDamage;
                magicSlier.max = maxTotal;
                magicSlier.value = sortedDamageInfos[i].MagicDamage;
                realSlier.max = maxTotal;
                realSlier.value = sortedDamageInfos[i].RealDamage;
            }
        }
    }
}
