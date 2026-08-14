using FairyGUI;
using FairyGUI.Utils;
using Spine.Unity;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.IsolatedStorage;
using System.Linq;
using System.Web;
using System.Windows.Forms;
using UnityEngine;

namespace DIY
{
    public partial class UI_Main : GComponent
    {
        private GoWrapper _currentWrapper;

        public int pageIndex = 0;
        public bool isNew = false;
        public bool isOp = false;
        public List<string> unitList = new List<string>();
        public string folderPath = UnityEngine.Application.streamingAssetsPath + "/Excel/";
        public List<string> folderList = new List<string>();
        public string folder = "";

        public UnitData selectUnit = null;
        public Dictionary<(string, string, string), int> unitInfos = new Dictionary<(string name, string type, string icon), int>();
        public int lastRow = -1;
        public Dictionary<string, int> unitIndexs = new Dictionary<string, int>();
        public Dictionary<int, string> skillNames = new Dictionary<int, string>();
        public Dictionary<int, string> selectedUnitNames = new Dictionary<int, string>();
        
        //public Dictionary<string, List<string>> attributeDic = ExcelHelper.dic;
        public Dictionary<string, Dictionary<string, int>> attributes = new Dictionary<string, Dictionary<string, int>>();
        public string[] unitTypes = new string[] { "中立单位", "干员", "敌人" };
        public string[] funPages = new string[] { "UnitData", "SkillData", "BuffData", "ModeifyData" };
        public string[][] attributeNames = new string[][] {
            new string[] {"Model/Model:string", "Name/Name:string", "Id/Id:string", "Type/Type:string", "血/Hp:int", "防/Defence:int", "魔防/MagicDefence:int", "攻/Attack:int", "不占用地板/NotUseTile:bool" },
            new string[] {"Model/Model:string", "Name/Name:string", "Id/Id:string", "Type/Type:string", "血/Hp:int", "防/Defence:int", "魔防/MagicDefence:int", "攻/Attack:int", "消耗/Cost:int", "阻挡个数/StopCount:int", "攻击间隔:AttackGap:float", "复活时间/ResetTime:int"},
            new string[] { "Model/Model:string", "Name/Name:string", "Id/Id:string", "Type/Type:string", "血/Hp:int", "防/Defence:int", "魔防/MagicDefence:int", "攻/Attack:int", "重量/Weight:int", "阻挡个数/StopCount:int", "攻击间隔:AttackGap:float" }
        };
        public IReadOnlyList<string> spineList => SpineResourceManager.Instance.AllSpineKeys.AsReadOnly();

        public string excelPath = "";
        public string model = "";
        public int attributeIndex = 0;
        List<Dictionary<(int, int), GLabel>> changeList = new List<Dictionary<(int row, int col), GLabel>>();

        partial void Init()
        {
            //unitList = ExcelHelper.GetUnitList();
            ExcelHelper.ExportClass(new List<string>() { UnityEngine.Application.streamingAssetsPath + "/Excel/Main/battle.xlsx" });
            List<string> folderpaths = Directory.GetDirectories(folderPath).ToList();
            folderList = Database.Instance.GetExcelPathNames(folderpaths);

            GameObject spineGo = ResHelper.Instantiate("Assets/Bundles/Units/char_002_amiya");
            spineGo.transform.localPosition = new Vector3(280, -150, 100);
            spineGo.transform.localRotation = Quaternion.Euler(-60, 0, 0);
            spineGo.transform.localScale = Vector3.one * 150;
            GGraph holder = m_spineMode.asGraph;
            _currentWrapper = new GoWrapper(spineGo);
            holder.SetNativeObject(_currentWrapper);

            m_exit.onClick.Add(() =>
            {
                UIManager.Instance.ChangeView<MainUI.UI_Main>(MainUI.UI_Main.URL);
            });
            m_Unit.onClick.Add(() =>
            {
                pageIndex = 0;
            });
            m_Skill.onClick.Add(() =>
            {
                pageIndex = 1;
            });
            m_isNewBtn.onChanged.Add(() =>
            {
                isNew = m_isNewBtn.selected;
                //if (isNew)
                freshChengeList2New();
            });
            m_folders.items = folderList.ToArray();
            m_folders.onChanged.Add(() =>
            {
                folder = folderList[m_folders.selectedIndex];
                freshExcel(folder);
            });
            m_excels.onChanged.Add(() =>
            {
#if UNITY_EDITOR
                excelPath = PathHelper.ExcelResPath + "/Excel/" + folder + "/" + m_excels.items[m_excels.selectedIndex];
#else
                excelPath = UnityEngine.Application.streamingAssetsPath + "/Excel/" + folder + "/" + m_excels.items[m_excels.selectedIndex];
#endif
                unitInfos = ExcelHelper.GetUnitList(excelPath);
                lastRow = unitInfos.Values.Last() + 1;
                searchItem();
                foreach (var i in unitInfos.Keys)
                {
                    unitIndexs[i.Item1] = unitInfos[i];
                }
            });
            m_unitNameInput.onChanged.Add(searchItem);
            //Debug.Log(attributeDic.Keys.ToList()[0]);
            //m_selectUnitAttribute.items = attributeDic["UnitData"].ToArray();
            m_unitType.onChanged.Add(() => {
                searchItem();
                freshAttribute();
                m_mode.alpha = 1;
                m_HP.m_text.text = "";
                m_Def.m_text.text = "";
                m_MicDef.m_text.text = "";
                m_Atk.m_text.text = "";
                m_Cost.m_text.text = "";
                m_OpStopCount.m_text.text = "";
                m_EnStopCount.m_text.text = "";
                m_OpAtkGap.m_text.text = "";
                m_EnAtkGap.m_text.text = "";
                m_ResetTime.m_text.text = "";
                m_Weight.m_text.text = "";
                m_NotUseTile.m_bool.selected = false;
                if (m_unitType.selectedPage == "opreator")
                    isOp = true;
                else
                    isOp = false;
                //m_selectUnitAttribute.items = attributeNames[m_unitType.selectedIndex];
            });
            m_isNewBtn.onChanged.Add(() =>
            {
                isNew = m_isNewBtn.selected;
            });
            m_mode.onClick.Add(() =>
            {
                //if ((m_excels.items?.Length ?? 0) == 0) return;
                //Debug.Log("mode");
                freshIcon();
                //m_mode.alpha = 0;
            });
            m_selectUnitCombobox.onChanged.Add(() =>
            {
                //if (isNew)
                m_mode.alpha = 1;
                m_unitName.m_text.text = m_selectUnitCombobox.items[m_selectUnitCombobox.selectedIndex].Split("/")[0];
                //if (m_unitType.selectedIndex != 0)
                //{
                int index = Database.Instance.GetIndex<UnitData>(m_selectUnitCombobox.items[m_selectUnitCombobox.selectedIndex].Split("/")[1]);
                selectUnit = Database.Instance.Get<UnitData>(index);
                string model = selectUnit.Model;
                string icon = selectUnit.HeadIcon;
                
                GameObject modelGo;
                loadModel(model, out modelGo);
                if (modelGo is not null && modelGo.GetComponentsInChildren<SkeletonAnimation>(true).Length > 0)
                {
                    SkeletonAnimation front = modelGo.transform.GetChild(1).GetComponent<SkeletonAnimation>();
                    front.loop = false;
                    front.AnimationName = "default";
                }
                // 更新渲染器缓存，以确保新的 GameObject 正确显示
                _currentWrapper.CacheRenderers();

                m_modeName.m_text.text = model;
                m_mode.alpha = 0;
                if (m_unitType.selectedIndex != 0)
                    m_unitIcon.url = "ui://Res/" + icon;
                else
                    m_unitIcon.url = "ui://Res/头像_装置_障碍物";
                //}
                m_HP.m_text.text = selectUnit.Hp.ToString();
                m_Def.m_text.text = selectUnit.Defence.ToString();
                m_MicDef.m_text.text = selectUnit.MagicDefence.ToString();
                m_Atk.m_text.text = selectUnit.Attack.ToString();
                m_Cost.m_text.text = selectUnit.Cost.ToString();
                m_OpStopCount.m_text.text = selectUnit.StopCount.ToString();
                m_EnStopCount.m_text.text = selectUnit.StopCount.ToString();
                m_OpAtkGap.m_text.text = selectUnit.AttackGap.ToString();
                m_EnAtkGap.m_text.text = selectUnit.AttackGap.ToString();
                m_ResetTime.m_text.text = selectUnit.ResetTime.ToString();
                m_Weight.m_text.text = selectUnit.Weight.ToString();
                m_NotUseTile.m_bool.selected = selectUnit.NotUseTile;
            });
            freshAttribute();
            m_newUnitAttribute.onClick.Add(addAttribute);

            m_save.onClick.Add(save2Excel);
            m_saveAsNew.onClick.Add(save2Excel);

            //SpineResourceManager.Instance.LoadAllSpineResources();
        }
        private void freshExcel(string folder)
        {
            m_excels.items = new string[0];
            string path = folderPath + $"/{folder}/";
            List<string> names = Directory.GetFiles(path).ToList().Select(x => Path.GetFileName(x)).ToList();
            List<string> todel = new List<string>();
            foreach (string name in names)
            {
                if (name.StartsWith("~$") || name.EndsWith(".meta"))
                    todel.Add(name);
            }
            names.RemoveAll(x => todel.Contains(x));
            if (names.Count == 0)
                m_tip.text = $"{m_tip.text}\n{folder}下没有Excel文件";
            m_excels.items = names.ToArray();
            //excelPath = UnityEngine.Application.streamingAssetsPath + "/Excel/" + folder + "/" + names[0];
#if UNITY_EDITOR
                excelPath = PathHelper.ExcelResPath + "/Excel/" + folder + "/" + names[0];
#else
            excelPath = UnityEngine.Application.streamingAssetsPath + "/Excel/" + folder + "/" + names[0];
#endif
            //Debug.Log(excelPath);
            unitInfos.Clear();
            unitInfos = ExcelHelper.GetUnitList(excelPath);
            lastRow = unitInfos.Values.Last() + 1;
            searchItem();
            attributes.Clear();
            //Debug.Log(attributes["UnitData"].ToString());
            foreach (var i in unitInfos.Keys)
            {
                unitIndexs[i.Item1] = unitInfos[i];
            }
        }
        private void searchItem()
        {
            //if (isNew) return;
            if (m_excels.items.Length == 0)
            {
                m_selectUnitCombobox.items = new string[0];
                return;
            }
            //unitInfos.Values.ToList().Select(x => x.Item2 = unitTypes[m_unitType.selectedIndex])
            Dictionary<int, string> unitList = new Dictionary<int, string>();
            //for (int i = 0; i < ExcelHelper.GetExcelRow(excelPath, "UnitData").row - 1; i++)
            //Debug.Log(ExcelHelper.GetExcelRow(excelPath, "UnitData").row - 1);
            //Debug.Log(unitInfos.Keys.ToList()[unitInfos.Count - 1]);
            foreach (var i in unitInfos.Keys)
            {
                if (i.Item2 == unitTypes[m_unitType.selectedIndex])
                    unitList[unitInfos[i]] = i.Item1;
            }
            if (m_unitNameInput.text == "")
            {
                //Debug.Log(unitInfos.Values.ToList()[0]);
                m_selectUnitCombobox.items = unitList.Values.ToArray();
            }
            else
            {
                List<string> result = unitList.Values.ToList().Where(x => x.Contains(m_unitNameInput.text)).ToList();
                m_selectUnitCombobox.items = result.ToArray();
            }
        }
        private void freshIcon()
        {
            var rows = spineList;
            int j = 0;
            m_icons.RemoveChildrenToPool();
            foreach (var i in rows)
            {
                string modelName = i.Split("/").Last();
                string iconName = modelName;
                if (modelName.StartsWith("char_"))
                {
                    iconName = "icon_" + modelName.Split("_")[2];
                }
                else if (modelName.StartsWith("enemy_"))
                {
                    
                }

                if (modelName == "") continue;
                m_icons.AddItemFromPool();
                GObject obj = m_icons.GetChildAt(j);
                //obj.asLabel.GetChild("icon").asLoader.url = "ui://Res/" + modelName;
                PackageItem icon = UIPackage.GetItemByURL("ui://Res/" + iconName);
                if (icon != null)
                {
                    obj.asLabel.icon = "ui://Res/" + iconName;
                }
                obj.asLabel.title = modelName;

                obj.onClick.Add(seclecIcon);
                //obj.data = unitInfos[i].Item1;
                obj.data = modelName;
                j++;
            }
        }
        private void seclecIcon(EventContext evt)
        {
            GObject obj = (GObject)evt.sender;
            m_unitIcon.url = obj.asLabel.GetChild("icon").asLoader.url;
            m_selectIcon.selectedIndex = 0;
            m_mode.alpha = 0;
            //string unitId = obj.data.ToString();
            //int modeliindex = Database.Instance.GetIndex<UnitData>(unitId);
            //model = Database.Instance.Get<UnitData>(modeliindex).Model;
            //Debug.Log(model);
            m_modeName.m_text.text = obj.data.ToString();

            GameObject model;
            loadModel(obj.data.ToString(), out model);

            if (model is not null && model.GetComponentsInChildren<SkeletonAnimation>(true).Length > 0)
            {
                SkeletonAnimation front = model.transform.GetChild(1).GetComponent<SkeletonAnimation>();
                front.loop = false;
                front.AnimationName = "default";
            }
            // 更新渲染器缓存，以确保新的 GameObject 正确显示
            _currentWrapper.CacheRenderers();
        }

        private void loadModel(string modelName, out GameObject model)
        {
            model = ResHelper.Instantiate("Assets/Bundles/Units/" + modelName);

            model.transform.localPosition = new Vector3(280, -150, 500);
            model.transform.localScale = Vector3.one * 150;

            if (model.GetComponentsInChildren<SkeletonAnimation>(true).Length > 0)
            {
                model.transform.localRotation = Quaternion.Euler(-60, 0, 0);

                SkeletonAnimation front = model.transform.GetChild(1).GetComponent<SkeletonAnimation>();
                
                front.loop = true;
                front.AnimationName = "Idle";

                SkeletonAnimation back = model.transform.Find("model_back")?.GetComponent<SkeletonAnimation>() ?? null;
                if (back is not null)
                {
                    back.AnimationName = "Idle";
                    back.gameObject.SetActive(false);
                }
            }
            else
            {
                model.transform.localRotation = Quaternion.Euler(-15, 45, -15);
            }

            // 4. 创建 GoWrapper 来包装这个 GameObject
            if (_currentWrapper.wrapTarget != null)
            {
                GameObject.Destroy(_currentWrapper.wrapTarget);
            }
            _currentWrapper.wrapTarget = model;

            // 更新渲染器缓存，以确保新的 GameObject 正确显示
            _currentWrapper.CacheRenderers();
        }

        private void freshAttribute()
        {
            attributes = ExcelHelper.GetAttributes(UnityEngine.Application.streamingAssetsPath + "/Excel/temp.xlsx");
            List<string> tmp = new List<string>();
            tmp.AddRange(attributes[funPages[pageIndex]].Keys.ToList());
            foreach (string s in attributeNames[m_unitType.selectedIndex])
            {
                tmp.Remove(s);
            }
            m_selectUnitAttribute.items = tmp.ToArray();
        }
        private void addAttribute()
        {
            string attribute = m_selectUnitAttribute.items[m_selectUnitAttribute.selectedIndex];
            int row = isNew ? lastRow : unitIndexs[m_selectUnitCombobox.items[m_selectUnitCombobox.selectedIndex]];
            int col = attributes[funPages[pageIndex]][attribute];
            Dictionary<(int, int), GLabel> change = new Dictionary<(int row, int col), GLabel>() {
                    { (row, col), new GLabel() }
            };
            //Debug.Log(change.Keys.ToList()[0]);
            bool flag = true;
            foreach (var i in changeList)
            {
                if (i.Keys.ToList()[0] == change.Keys.ToList()[0])
                {
                    flag = false;
                    m_tip.text = m_tip.text + "\n" + attribute + "已经添加过";
                    break;
                }
            }
            if (flag)
            {
                string data = ExcelHelper.GetCellData(excelPath, funPages[pageIndex], change.Keys.ToList()[0]);
                m_addUnitAttribute.AddItemFromPool();
                bool isBool = attribute.Split(":")[1] == "bool";
                GLabel obj = m_addUnitAttribute.GetChildAt(m_addUnitAttribute.numItems - 1).asLabel;
                change[(row, col)] = obj.asLabel;
                obj.asLabel.text = attribute.Split(":")[0] + ":\n" + attribute.Split(":")[1];
                if (isBool)
                {
                    obj.asLabel.GetController("type").selectedPage = "bool";
                    obj.asLabel.GetChild("bool").asButton.selected = data != "" ? true : false;
                    //obj.GetChild("bool").asButton.onChanged.Add(saveAttribute2List);
                }
                else
                {
                    obj.asLabel.GetController("type").selectedPage = "text";
                    obj.asLabel.GetChild("text").asTextInput.text = data;
                    //obj.GetChild("text").asTextInput.onChanged.Add(saveAttribute2List);
                }
                changeList.Add(change);
            }
            else
                return;
        }
        private void save2Excel(EventContext evt)
        {
            GButton button = (GButton)evt.sender;
            string mode = button.GetChild("title").text;
            bool flag = mode == "保存" ? true : false;
            string sheetName = funPages[pageIndex];
            List<(int row, int col, string data)> dataList = new List<(int row, int col, string data)>();
            int _row = isNew ? lastRow : unitIndexs[m_selectUnitCombobox.items[m_selectUnitCombobox.selectedIndex]];
            dataList.Add((_row, 11, m_HP.m_text.text));
            dataList.Add((_row, 15, m_Def.m_text.text));
            dataList.Add((_row, 17, m_MicDef.m_text.text));
            dataList.Add((_row, 13, m_Atk.m_text.text));
            dataList.Add((_row, 19, m_Cost.m_text.text));
            if (m_OpAtkGap.m_text.text == "")
                dataList.Add((_row, 26, m_EnAtkGap.m_text.text));
            else
                dataList.Add((_row, 26, m_OpAtkGap.m_text.text));
            if (m_OpStopCount.m_text.text == "")
                dataList.Add((_row, 41, m_EnStopCount.m_text.text));
            else
                dataList.Add((_row, 41, m_OpStopCount.m_text.text));
            dataList.Add((_row, 22, m_ResetTime.m_text.text));
            dataList.Add((_row, 27, m_Weight.m_text.text));
            dataList.Add((_row, 43, m_NotUseTile.m_bool.selected ? "1" : "0"));
            dataList.Add((_row, 56, m_unitIcon.url.Split("/").Last()));
            dataList.Add((_row, 6, m_modeName.m_text.text));
            dataList.Add((_row, 33, m_unitName.m_text.text));
            if (isOp)
            {
                string unitId = ExcelHelper.GetCellData(excelPath, "UnitData", (_row, 1));
                if (unitId != m_unitName.m_text.text && unitId != "")
                {
                    dataList.Add((0, 0, unitId));
                    m_tip.text = m_tip.text + "\n检测到UnitName变更,已自动同步到CardData";
                }
            }
            //if (isNew)
            //{
            //    dataList.Add(());
            //}
            foreach (var i in changeList)
            {
                (int row, int col) = flag ? i.Keys.ToList()[0] : (4, i.Keys.ToList()[0].Item2);
               GLabel label = i.Values.ToList()[0];
               string data = "";
                if (label.GetController("type").selectedPage == "bool")
                {
                    data = label.GetChild("bool").asButton.selected ? "1" : "0";
                }
                else
                {
                    data = label.GetChild("text").asTextInput.text;
                }
                dataList.Add((row, col, data));
            }
            if (flag)
            {
                m_tip.text = m_tip.text + "\n" + ExcelHelper.ModifyExcel(excelPath, sheetName, dataList,
                    isNew ? unitIndexs[m_selectUnitCombobox.items[m_selectUnitCombobox.selectedIndex]] : 0,
                    isOp);
                lastRow++;
            }
            else
            {
                string newExcelPath = $"{UnityEngine.Application.streamingAssetsPath}/Excel/{folder}/新单位_{m_unitName.m_text.text}.xlsx";
                dataList.Add((4, 1, $"新单位_{m_unitName.m_text.text}"));
                m_tip.text = m_tip.text + "\n另存为ing...";
                m_tip.text = m_tip.text + "\n" + ExcelHelper.CreatExcel(newExcelPath, sheetName, dataList,
                    excelPath, unitIndexs[m_selectUnitCombobox.items[m_selectUnitCombobox.selectedIndex]],
                    isOp);
            }
        }
        private void saveAsNewExcel()
        {
            
        }
        private void freshChengeList2New()
        {
            List<Dictionary<(int, int), GLabel>> toRemove = new List<Dictionary<(int, int), GLabel>>();
            List<Dictionary<(int, int), GLabel>> toAdd = new List<Dictionary<(int, int), GLabel>>();
            int nowSelect = unitIndexs[m_selectUnitCombobox.items[m_selectUnitCombobox.selectedIndex]];
            foreach (var i in changeList)
            {
                (int row, int col) pos = (isNew ? lastRow : nowSelect,i.Keys.ToList()[0].Item2);
                Dictionary<(int, int), GLabel> change = new Dictionary<(int row, int col), GLabel>() { { pos, i.Values.ToList()[0] } };
                toAdd.Add(change);
                toRemove.Add(i);
            }
            foreach (var i in toRemove)
            {
                changeList.Remove(i);
            }
            foreach (var i in toAdd)
            {
                changeList.Add(i);
            }
        }
    }
}