using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using FairyGUI;
using UnityEngine;
using System.Windows.Forms;

namespace MainUI
{
    partial class UI_Main : IGameUIView
    {
        GameData gameData => GameData.Instance;
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
            onRightClick.Add(async () =>
            {
                var ui = UIManager.Instance.ChangeView<DungeonUI.UI_Dialogue>(DungeonUI.UI_Dialogue.URL);
                await ui.StartDialogue("初始事件");
                UIManager.Instance.ChangeView<GComponent>(URL);
            });
            m_Name.onFocusOut.Add(() =>
            {
                if (GameData.Instance.Name != m_Name.text)
                {
                    GameData.Instance.Name = m_Name.text;
                    SaveHelper.SaveData();
                }
            });
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
                    await Database.Instance.Init();
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
            m_importSpine.onClick.Add(OpenFolderDialog);
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
            if (gameData.Teams[0].Cards.Count > 0)
            {
                string picName = Database.Instance.Get<UnitData>(gameData.Teams[0].Cards[0].UnitId).StandPic;
                m_standPic.texture = new NTexture(ResHelper.GetAsset<Texture>(PathHelper.StandPicPath + picName));
            }
        }
        public void ExccelListClicke(GTreeNode node)
        {
            GComponent self = node.cell;
            string data = node.text;
            int index = self.GetChild("selectBtn").asButton.GetController("button").selectedIndex;
            if (node.numChildren > 0)
            {
                node.expanded = true;
                for (int i = 0; i < node.numChildren; i++)
                {
                    GTreeNode sonNode = node.GetChildAt(i);
                    GComponent obj = sonNode.cell;
                    obj.GetChild("selectBtn").asButton.GetController("button").selectedIndex = index;
                    ExccelListClicke(sonNode);
                }
            }
            else
            { 
                GComponent obj = node.cell;
                int flag = obj.GetChild("selectBtn").asButton.GetController("button").selectedIndex;
                //Debug.Log("button index:" + flag);
                string path = obj.GetChild("path").text;
                if (flag == 0)
                {
                    if (ExcelList.Contains(path)) ExcelList.Remove(path);
                }
                else
                { 
                    if (!ExcelList.Contains(path)) ExcelList.Add(path);
                }
                SaveHelper.SaveData();
                freshNode();
                //foreach (string i in ExcelList)
                //{
                //    Debug.Log(i);
                //}
            }
        }

        public void TreeViewInit()
        {
            rootNode = m_ExcelList.rootNode;
            rootNode.RemoveChildren();
            List<string> ExcelFolderPaths = Database.Instance.GetExcelPathList();
            List<string> ExcelFolderNames = new List<string>();
            List<string> ExcelFilePaths = new List<string>();
            List<string> ExcelFileNames = new List<string>();
            ExcelFolderNames.AddRange(ExcelFolderPaths.Select(x => System.IO.Path.GetFileNameWithoutExtension(x)));

            for (int i = 0; i < ExcelFolderNames.Count; i++)
            {
                //Debug.Log(ExcelFolderNames[i]);
                GTreeNode item_folder = new GTreeNode(true);
                rootNode.AddChild(item_folder);
                //Debug.Log(item_folder.level);
                GComponent obj_folder = item_folder.cell;
                obj_folder.GetChild("title").text = ExcelFolderNames[i];
                obj_folder.GetChild("selectBtn").asButton.onClick.Add(() =>
                {
                    ExccelListClicke(item_folder);
                });
                ExcelFilePaths.AddRange(Database.Instance.GetExcelFileList(ExcelFolderPaths[i]));
                ExcelFileNames.AddRange(ExcelFilePaths.Select(x => System.IO.Path.GetFileNameWithoutExtension(x)));
                item_folder.expanded = true;
                for (int j = 0; j < ExcelFileNames.Count; j++)
                {
                    //Debug.Log(ExcelFileNames[j]);
                    GTreeNode item_file = new GTreeNode(false);
                    string path = ExcelFolderPaths[i] + "\\" + ExcelFileNames[j] + ".xlsx";
                    rootNode.AddChild(item_file);
                    //Debug.Log(item_folder.GetChildAt(j).level);
                    GComponent obj_file = item_file.cell;
                    obj_file.GetChild("title").text = ExcelFileNames[j];
                    obj_file.GetChild("path").text = path;
                    //Debug.Log(obj_file.GetChild("path").text);
                    //Debug.Log(obj_file.GetChild("title").text);
                    obj_file.GetChild("selectBtn").asButton.onClick.Add(() =>
                    {
                        ExccelListClicke(item_file);
                    });
                    if (ExcelList.Contains(path))
                    {
                        obj_file.GetChild("selectBtn").asButton.GetController("button").selectedIndex = 1;
                    }
                    item_folder.AddChild(item_file);
                    rootNode.RemoveChild(item_file);

                    //Debug.Log();
                    //item_file.text = ExcelFileNames[j];
                    //Debug.Log(item_file.GetChildAt(0));
                }
                ExcelFileNames.Clear();
                ExcelFilePaths.Clear();
            }
        }
        public void freshNode()
        {
            for (int i = 0; i < m_ExcelList.rootNode.numChildren; i++)
            {
                GTreeNode node = m_ExcelList.rootNode.GetChildAt(i);
                if (node.numChildren > 0)
                {
                    int flag = 0;
                    for (int j = 0; j < node.numChildren; j++)
                    {
                        GTreeNode sonNode = node.GetChildAt(j);
                        GComponent obj = sonNode.cell;
                        flag += obj.GetChild("selectBtn").asButton.GetController("button").selectedIndex;
                    }
                    GComponent self = node.cell;
                    if (flag == 0) self.GetChild("selectBtn").asButton.GetController("button").selectedIndex = 0;
                    else if (flag == node.numChildren) self.GetChild("selectBtn").asButton.GetController("button").selectedIndex = 1;
                }
            }
        }
        public void OpenFolderDialog()
        {
            var folderDialog = new FolderBrowserDialog();
            folderDialog.Description = "选择文件夹";
            folderDialog.SelectedPath = Path.Combine(UnityEngine.Application.streamingAssetsPath, "Spine").Replace("/", "\\"); // 默认路径
            //folderDialog.SelectedPath = UnityEngine.Application.dataPath; // 默认路径
            //folderDialog.SelectedPath = "D:\\UnityWork\\rebuild\\ArknightN_Data\\StreamingAssets"; // 默认路径
            Debug.Log("默认路径: " + folderDialog.SelectedPath);

            if (folderDialog.ShowDialog() == DialogResult.OK)
            {
                string path = folderDialog.SelectedPath;
                Debug.Log("文件夹路径: " + path);
                // 处理路径（例如遍历文件）
            }
        }
    }
}
