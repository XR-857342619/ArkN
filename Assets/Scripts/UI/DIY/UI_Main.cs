using FairyGUI;
using FairyGUI.Utils;
using Newtonsoft.Json.Linq;
using Spine.Unity;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.IsolatedStorage;
using System.Linq;
using UnityEngine;

namespace DIY
{

    public partial class UI_Main : GComponent
    {
//#if !UNITY_ANDROID
        private GoWrapper _currentWrapper;

        public int pageIndex = 0;
        public bool isNew = false;
        public bool isOp = false;
        public List<string> unitList = new List<string>();

        /// <summary>StreamingAssets/Data 下的配置根目录。</summary>
        public string dataRootPath = UnityEngine.Application.streamingAssetsPath + "/Data";
        /// <summary>当前选中的配置文件完整路径。</summary>
        public string jsonFilePath = "";
        /// <summary>当前配置文件解析出的全部 UnitData。</summary>
        public List<UnitData> currentUnitDatas = new List<UnitData>();
        /// <summary>Id -> UnitData，供下拉框选中后直接使用，不依赖 Database。</summary>
        public Dictionary<string, UnitData> unitDataById = new Dictionary<string, UnitData>();

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

        public string model = "";
        public int attributeIndex = 0;
        List<Dictionary<(int, int), GLabel>> changeList = new List<Dictionary<(int row, int col), GLabel>>();

        partial void Init()
        {
            GameObject spineGo = ResHelper.Instantiate("Assets/Bundles/Units/char_002_amiya");
            spineGo.transform.localPosition = new Vector3(280, -340, 100);
            spineGo.transform.localRotation = Quaternion.Euler(-60, 0, 0);
            spineGo.transform.localScale = Vector3.one * 220;
            GGraph holder = m_spineMode.asGraph;
            _currentWrapper = new GoWrapper(spineGo);
            holder.SetNativeObject(_currentWrapper);

            InitAnimationControls();
            SetupPreviewAnimations(spineGo);
            _currentWrapper.CacheRenderers();

            m_exit.onClick.Add(() =>
            {
                UIManager.Instance.ChangeView<MainUI.UI_Main>(MainUI.UI_Main.URL);
            });
#if !UNITY_ANDROID
            m_UnitModel.onClick.Add(() =>
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
            m_folders.onChanged.Add(() =>
            {
                if (m_folders.items == null || m_folders.items.Length == 0)
                    return;

                int index = Mathf.Clamp(m_folders.selectedIndex, 0, folderList.Count - 1);
                folder = folderList[index];
                freshJsonFiles(folder);
            });
            m_jsonFiles.onChanged.Add(OnJsonFileChanged);
            m_unitNameInput.onChanged.Add(searchItem);
            m_unitType.onChanged.Add(() =>
            {
                isOp = m_unitType.selectedPage == "opreator";
                freshAttribute();
                searchItem();
            });

            // 初始化 StreamingAssets/Data 下的配置文件夹与 JSON 文件列表
            RefreshFolderList();
            m_isNewBtn.onChanged.Add(() =>
            {
                isNew = m_isNewBtn.selected;
            });
#endif
            m_mode.onClick.Add(() =>
            {
                //if ((m_excels.items?.Length ?? 0) == 0) return;
                //Debug.Log("mode");
                freshIcon();
                //m_mode.alpha = 0;
            });
#if !UNITY_ANDROID
            m_selectUnitCombobox.onChanged.Add(OnSelectedUnitChanged);
            //freshAttribute();
            m_newUnitAttribute.onClick.Add(addAttribute);


            m_save.onClick.Add(save2Excel);
            m_saveAsNew.onClick.Add(save2Excel);


            //SpineResourceManager.Instance.LoadAllSpineResources();
#endif
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
        }

        private void loadModel(string modelName, out GameObject model)
        {
            model = ResHelper.Instantiate("Assets/Bundles/Units/" + modelName);

            if (model == null)
            {
                ClearPreviewAnimations();
                return;
            }

            model.transform.localPosition = new Vector3(280, -340, -220);
            model.transform.localScale = Vector3.one * 220;

            if (model.GetComponentsInChildren<SkeletonAnimation>(true).Length > 0)
            {
                model.transform.localRotation = Quaternion.Euler(-60, 0, 0);
                SetupPreviewAnimations(model);
            }
            else
            {
                model.transform.localRotation = Quaternion.Euler(-15, 45, -15);
                ClearPreviewAnimations();
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

#if !UNITY_ANDROID
        private void freshAttribute()
        {
            // 属性元数据已与 Excel 解绑，后续从 DiySchema.json 读取。
            // 在 JSON 属性系统接入前，先清空下拉框，避免继续访问 Excel 模板。
            attributes.Clear();
            if (m_selectUnitAttribute != null)
                m_selectUnitAttribute.items = new string[0];
        }


        private void addAttribute()
        {
            if (m_selectUnitAttribute == null ||
                m_selectUnitAttribute.items == null ||
                m_selectUnitAttribute.items.Length == 0)
            {
                m_tip.text = m_tip.text + "\n属性配置尚未从 DiySchema.json 加载";
                return;
            }

            if (m_selectUnitCombobox.items == null || m_selectUnitCombobox.items.Length == 0)
                return;

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
                // 属性值后续从 JSON schema / 选中记录的 JObject 中读取。
                string data = "";
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
            // 当前读取链路已经切换到 StreamingAssets/Data 下的 JSON/TXT。
            // 保存链路将在后续步骤迁移到 JSON。
            m_tip.text = m_tip.text + "\n保存功能尚未迁移到 JSON 数据源";
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
#endif
    }
}

