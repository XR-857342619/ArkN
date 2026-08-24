using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using FairyGUI;
using UnityEngine;

namespace MainUI
{
    partial class UI_Main : IGameUIView
    {
        GameData gameData => GameData.Instance;
        //List<string> ExcelList => GameData.Instance.ExcelList;
        List<string> ExcelList = GameData.Instance.ExcelList;
        public GTreeNode rootNode;
        partial void Init()
        {
            m_Map.onClick.Add(() =>
            {
                UIManager.Instance.ChangeView<MapBuilderUI.UI_MapBuilder>(MapBuilderUI.UI_MapBuilder.URL);
            });
            m_member.onClick.Add(() =>
            {
                UIManager.Instance.ChangeView<UI_MemberPage>(UI_MemberPage.URL);
            });
            m_team.onClick.Add(async () =>
            {
                var uiTeam = UIManager.Instance.ChangeView<UI_Team>(UI_Team.URL);
                uiTeam.IfGoBattle(false);
                await uiTeam.ChooseTeam();
                UIManager.Instance.ChangeView<GComponent>(URL);
            });
            m_battle.onClick.Add(() =>
            {
                UIManager.Instance.ChangeView<UI_Battle>(UI_Battle.URL);
            });
            m_rogue.onClick.Add(() =>
            {
                //    DungeonManager.Instance.PrepareDungeon();
                UIManager.Instance.ChangeView<DIY.UI_Main>(DIY.UI_Main.URL);
            });
            //onRightClick.Add(async () =>
            //{
            //    var ui = UIManager.Instance.ChangeView<DungeonUI.UI_Dialogue>(DungeonUI.UI_Dialogue.URL);
            //    await ui.StartDialogue("初始事件");
            //    UIManager.Instance.ChangeView<GComponent>(URL);
            //});
            m_Name.onFocusOut.Add(() =>
            {
                if (GameData.Instance.Name != m_Name.text)
                {
                    GameData.Instance.Name = m_Name.text;
                    SaveHelper.SaveData();
                }
            });
            //m_refresh.onClick.Add(() =>
            //{
            //    //Database.Instance.
            //});
            m_close.onClick.Add(() =>
            {
                SaveHelper.SaveData();
            });
            m_Setting.onClick.Add(() =>
            {
                m_settingC.selectedIndex = 1;
            });
            m_close.onClick.Add(() =>
            {
                m_settingC.selectedIndex = 0;
                SaveHelper.SaveData();
            });
            m_bgm.onChanged.Add(() =>
            {
                GameData.Instance.Bgm = (float)m_bgm.value / 100f;
            });
            m_Export.onClick.Add(async () =>
            {
                //foreach (KeyValuePair<string, bool> kvp in GameData.Instance.excelDict)
                //{
                //    if (kvp.Value)
                //    {
                //        excelist.Add(kvp.Key);
                //    }
                //}
                if (ExcelList.Count > 0)
                {
                    foreach (string path in ExcelList)
                    { 
                        //Debug.Log(path);
                    }
                    ExcelHelper.Export(ExcelList);
                    Database.Instance.Clear();
                    UnifiedExpressionEngine.ClearCache();
                    await Database.Instance.Init();
                    GameData.Instance.RefreshCardData();
                    TipManager.Instance.ShowTip("导表结束");
                }
                else
                {
                    TipManager.Instance.ShowTip("请选择表格");
                }
            });
            m_ShowHp.onClick.Add(() =>
            {
                if (m_ShowHp.selected)
                {
                    GameData.Instance.showHP = true;
                }
                else
                {
                    GameData.Instance.showHP = false;
                }
                SaveHelper.SaveData();
            });
            m_ShowElement.onClick.Add(() =>
            {
                if (m_ShowElement.selected)
                {
                    GameData.Instance.showElement = true;
                }
                else
                {
                    GameData.Instance.showElement = false;
                }
                SaveHelper.SaveData();
            });
            //m_importSpine.onClick.Add(OpenFolderDialog);
            //GTree exceltree = m_ExcelList;
            //GTreeNode rootNode = exceltree.rootNode;
            //TreeViewInit();
            if (TipManager.Instance.initErorrTips.Count > 0)
            {
                m_InitError.text += "喜报:\n初始化错误\n";
                foreach (string i in TipManager.Instance.initErorrTips)
                {
                    TipManager.Instance.ShowTip(i);
                    m_InitError.text += i + "\n";
                }
            }
            m_ExportBtn.onClick.Add(() =>
            {
                TreeViewInit();
                //freshNode();
            });
        }

        protected override void OnUpdate()
        {
            base.OnUpdate();
        }

        public void Enter()
        {
            Flush();
        }

        public void Flush()
        {
            m_bgm.value = GameData.Instance.Bgm * 100;
            m_ShowElement.selected = GameData.Instance.showElement;
            m_ShowHp.selected = GameData.Instance.showHP;
            m_Name.text = GameData.Instance.Name;
            m_Version.text = UnityEngine.Application.version;
            var cards = gameData?.Teams[0]?.Cards ?? new List<Card>();
            if (cards.Count > 0)
            {
                string picName = Database.Instance.Get<UnitData>(cards[0].UnitId).StandPic;
                m_standPic.texture = new NTexture(ResHelper.GetAsset<Texture>(PathHelper.StandPicPath + picName));
            }
        }
        public void ExccelListClicke(GTreeNode node)
        {
            if (node == null || node.cell == null) return;

            if (node.numChildren > 0)
            {
                // 文件夹：根据当前子文件状态计算目标状态，避免依赖 GButton 自动切换导致的“多次点击才生效”问题
                bool allOn = true;
                for (int i = 0; i < node.numChildren; i++)
                {
                    GTreeNode sonNode = node.GetChildAt(i);
                    if (sonNode == null || sonNode.cell == null) continue;
                    if (sonNode.cell.GetChild("selectBtn").asButton.GetController("button").selectedIndex != 1)
                    {
                        allOn = false;
                        break;
                    }
                }

                int target = allOn ? 0 : 1;
                node.cell.GetChild("selectBtn").asButton.GetController("button").selectedIndex = target;

                node.expanded = true;
                for (int i = 0; i < node.numChildren; i++)
                {
                    GTreeNode sonNode = node.GetChildAt(i);
                    if (sonNode == null || sonNode.cell == null) continue;

                    GComponent obj = sonNode.cell;
                    obj.GetChild("selectBtn").asButton.GetController("button").selectedIndex = target;

                    // 实际路径存储在节点 data 中，避免显示短路径导致逻辑读取错误
                    string path = sonNode.data as string;
                    if (string.IsNullOrEmpty(path)) continue;
                    if (target == 0)
                    {
                        if (ExcelList.Contains(path)) ExcelList.Remove(path);
                    }
                    else
                    {
                        if (!ExcelList.Contains(path)) ExcelList.Add(path);
                    }
                }
                SaveHelper.SaveData();
                freshNode();
            }
            else
            {
                GComponent obj = node.cell;
                // 实际路径存储在节点 data 中，path 文本仅用于显示短路径
                string path = node.data as string;
                if (string.IsNullOrEmpty(path)) return;

                // 文件：根据 ExcelList 当前状态取反，不依赖 GButton 自动切换
                int target = ExcelList.Contains(path) ? 0 : 1;
                obj.GetChild("selectBtn").asButton.GetController("button").selectedIndex = target;

                if (target == 0)
                {
                    if (ExcelList.Contains(path)) ExcelList.Remove(path);
                }
                else
                {
                    if (!ExcelList.Contains(path)) ExcelList.Add(path);
                }

                SaveHelper.SaveData();
                // 文件点击后刷新所在文件夹的状态
                freshNode();
            }
        }

        public void TreeViewInit()
        {
            rootNode = m_ExcelList.rootNode;
            rootNode.RemoveChildren();
            List<string> ExcelFolderPaths = Database.Instance.GetExcelPathList();
            List<string> ExcelFolderNames = new List<string>();
            ExcelFolderNames.AddRange(ExcelFolderPaths.Select(x => System.IO.Path.GetFileNameWithoutExtension(x)));

            for (int i = 0; i < ExcelFolderNames.Count; i++)
            {
                GTreeNode item_folder = new GTreeNode(true);
                rootNode.AddChild(item_folder);
                GComponent obj_folder = item_folder.cell;
                obj_folder.GetChild("title").text = ExcelFolderNames[i];
                // 文件夹节点显示完整绝对路径，便于定位
                obj_folder.GetChild("path").text = PathHelper.NormalizeAppPath(ExcelFolderPaths[i]);
                // 使用 Set 替换监听，避免重复点击 m_ExportBtn 时累积多个回调导致状态错乱
                obj_folder.GetChild("selectBtn").asButton.onClick.Set(() =>
                {
                    ExccelListClicke(item_folder);
                });

                // 每个文件夹独立保存文件列表，避免跨文件夹累积
                List<string> ExcelFilePaths = Database.Instance.GetExcelFileList(ExcelFolderPaths[i]);
                List<string> ExcelFileNames = ExcelFilePaths
                    .Select(x => System.IO.Path.GetFileNameWithoutExtension(x))
                    .ToList();

                item_folder.expanded = true;
                for (int j = 0; j < ExcelFileNames.Count; j++)
                {
                    GTreeNode item_file = new GTreeNode(false);
                    // 统一保存为规范化绝对路径，避免 data.sav 中路径缺少 /storage/emulated/0 前缀
                    string path = PathHelper.NormalizeAppPath(System.IO.Path.Combine(ExcelFolderPaths[i], ExcelFileNames[j] + ".xlsx"));
                    // 旧版 FairyGUI 需要节点先加入树后 cell 才可用，因此先 AddChild 再访问 cell
                    item_folder.AddChild(item_file);
                    // 完整路径存入节点 data，path 文本仅显示短路径，逻辑读取统一走 data
                    item_file.data = path;
                    GComponent obj_file = item_file.cell;
                    obj_file.GetChild("title").text = ExcelFileNames[j];
                    obj_file.GetChild("path").text = GetShortExcelDisplayPath(path);
                    obj_file.GetChild("selectBtn").asButton.onClick.Set(() =>
                    {
                        ExccelListClicke(item_file);
                    });
                    if (ExcelList.Contains(path))
                    {
                        obj_file.GetChild("selectBtn").asButton.GetController("button").selectedIndex = 1;
                    }
                }
            }

            // 首次构建完成后刷新一次文件夹状态，使文件夹按钮与子 Excel 表格按钮保持一致
            freshNode();
        }

        /// <summary>
        /// 生成树状视图中显示的短路径（如 Main/battle.xlsx），不影响实际存储的完整路径。
        /// </summary>
        private static string GetShortExcelDisplayPath(string fullPath)
        {
            if (string.IsNullOrEmpty(fullPath)) return fullPath;

            string normalized = fullPath.Replace('\\', '/');
            int idx = normalized.IndexOf("/Excel/", System.StringComparison.OrdinalIgnoreCase);
            if (idx >= 0)
                return normalized.Substring(idx + "/Excel/".Length);

            return System.IO.Path.GetFileName(fullPath);
        }
        public void freshNode()
        {
            for (int i = 0; i < m_ExcelList.rootNode.numChildren; i++)
            {
                GTreeNode node = m_ExcelList.rootNode.GetChildAt(i);
                if (node == null || node.numChildren <= 0 || node.cell == null) continue;

                bool anySelected = false;
                for (int j = 0; j < node.numChildren; j++)
                {
                    GTreeNode sonNode = node.GetChildAt(j);
                    if (sonNode == null || sonNode.cell == null) continue;
                    GComponent obj = sonNode.cell;
                    if (obj.GetChild("selectBtn").asButton.GetController("button").selectedIndex == 1)
                    {
                        anySelected = true;
                        break;
                    }
                }

                GComponent self = node.cell;
                self.GetChild("selectBtn").asButton.GetController("button").selectedIndex = anySelected ? 1 : 0;
            }
            Debug.Log("刷新完成");
            foreach (string path in ExcelList)
            {
                Debug.Log("已选择的表格: " + path);
            }
        }
        //public void OpenFolderDialog()
        //{
        //    var folderDialog = new FolderBrowserDialog();
        //    folderDialog.Description = "选择文件夹";
        //    folderDialog.SelectedPath = Path.Combine(UnityEngine.Application.streamingAssetsPath, "Spine").Replace("/", "\\"); // 默认路径
        //    //folderDialog.SelectedPath = UnityEngine.Application.dataPath; // 默认路径
        //    //folderDialog.SelectedPath = "D:\\UnityWork\\rebuild\\ArknightN_Data\\StreamingAssets"; // 默认路径
        //    Debug.Log("默认路径: " + folderDialog.SelectedPath);

        //    if (folderDialog.ShowDialog() == DialogResult.OK)
        //    {
        //        string path = folderDialog.SelectedPath;
        //        Debug.Log("文件夹路径: " + path);
        //        // 处理路径（例如遍历文件）
        //    }
        //}
    }
}