using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FairyGUI;
using UnityEngine;

namespace MainUI
{
    partial class UI_Battle:IGameUIView
    {
        TaskCompletionSource<bool> goTcs;
        List<int> contracts = new List<int>();
        string mapPackageName = "Main";
        bool isEdit = false;
        bool isMove = false;
        bool isLink = false;
        bool isPreview = false;
        List<Vector2> linkset = new List<Vector2>();
        Config Config = new Config();
        List<string> ExcelList = new List<string>();
        partial void Init()
        {
            m_back.onClick.Add(() =>
            {
                UIManager.Instance.ChangeView<UI_Main>(UI_Main.URL);
                m_showLevelInfo.selectedIndex = 0;
            });

            m_MapPackage.onChanged.Add(() =>
            {
                mapPackageName = m_MapPackage.text;
                freshMaps();
            });

            m_MapPackage.selectedIndex = 0;
            m_MapPackage.items = m_MapPackage.values = Database.Instance.GetMapPackages().ToArray();
            //foreach (var child in m_world.GetChildren())
            //{
            //    if (child is UI_BattleInfo)
            //        child.onClick.Add(MapClick);
            //}
            m_levelInfo.m_start.onClick.Add(() => 
            {
                goTcs.TrySetResult(true);
                BattleManager.Instance.IsPreview = false;
            });
            //m_world.onClick.Add(cancelLevelInfo);
            m_world.draggable = true;
            m_world.onDragStart.Add((x) =>
            {
                x.PreventDefault();
                cancelLevelInfo();
            });

            m_EditorMod.onClick.Add(() =>
            { 
                isEdit = true;
                cancelLevelInfo();
            });
            m_MapMove.onClick.Add(() =>
            {
                isMove = true;
                foreach (var child in m_world.m_levelBack.GetChildren())
                {
                    child.draggable = true;
                }
            });
            m_MapLink.onClick.Add(() =>
            {
                isLink = true;
            });
            m_DelLink.onClick.Add(() =>
            {
                //Debug.Log("删除连线");
                for (int i = 0; i < m_world.m_links.numChildren; i++)
                {
                    m_world.m_links.GetChildAt(i).Dispose();
                }
                m_world.m_links.RemoveChildren();
                Config.lines.Clear();
                //freshMaps();
            });
            m_Linkcomfirm.onClick.Add(() =>
            {
                if (linkset.Count < 2) return;
                GGraph line = new GGraph();
                line.SetSize(100, 100);
                line.DrawPolygon(100.0f, 100.0f, GetRectVertices(linkset), Color.white);
                m_world.m_links.AddChild(line);
                Config.lines.Add(GetRectVertices(linkset));
                foreach (var child in m_world.m_levelBack.GetChildren())
                {
                    child.asCom.GetController("isLinkItem").selectedIndex = 0;
                }
                linkset.Clear();
                isLink = false;
                //SaveHelper.SavePackageConfig(mapPackageName, Config);
            });
            m_Movecomfirm.onClick.Add(() =>
            {
                foreach (var child in m_world.m_levelBack.GetChildren())
                {
                    child.draggable = false;
                    string name = child.data as string;
                    Config.positions[name] = child.position;
                }
                //SaveHelper.SavePackageConfig(mapPackageName, Config);
                isMove = false;
            });
            m_Excelcomfirm.onClick.Add(() =>
            {
                //Config.excels.AddRange(ExcelList.Distinct().ToList());
            });
            m_Editcomfirm.onClick.Add(() =>
            { 
                SaveHelper.SavePackageConfig(mapPackageName, Config);
                isEdit = false;
                ExcelList.Clear();
                freshMaps();
                //Debug.Log(Config.lines.ToString());
            });

            //m_levelInfo.m_Train.onClick.Add(() => m_contractChoose.selectedIndex = 1);
            m_levelInfo.m_Train.onClick.Add(() =>
            {
                goTcs.TrySetResult(true);
                BattleManager.Instance.IsPreview = true;
            });
            m_contractBack.onClick.Add(() => m_contractChoose.selectedIndex = 0);

            m_contracts.RemoveChildrenToPool();
            ContractData[] array = Database.Instance.GetAll<ContractData>();
            //for (int i = 0; i < array.Length; i++)
            //{
            //    int k = i;
            //    ContractData cData = array[i];
            //    var uiContract = m_contracts.AddItemFromPool() as DungeonUI.UI_BattleContract;
            //    uiContract.m_icon.icon = cData.Icon.ToContractIcon();
            //    uiContract.m_TagName.text = cData.Name;
            //    uiContract.onClick.Add(() => 
            //    {
            //        if (contracts.Contains(k)) contracts.Remove(k);
            //        else contracts.Add(k); freshContract();
            //    });
            //}
            //freshContract();
        }

        void freshContract()
        {
            for (int i = 0; i < m_contracts.numItems; i++)
            {
                var uiContract = m_contracts.GetChildAt(i) as DungeonUI.UI_BattleContract;
                uiContract.m_button.selectedIndex = contracts.Contains(i) ? 0 : 1;
            }
        }

        void cancelLevelInfo()
        {
            if (m_showLevelInfo.selectedIndex == 1)
            {
                goTcs.TrySetResult(false);
                m_showLevelInfo.selectedIndex = 0;
            }
        }
        void MapDrag(EventContext evt)
        {
            if (isEdit)
            {
                if (isMove)
                {   
                    //Debug.Log("移动");
                    var sender = evt.sender as GObject;
                    Vector2 mousePos = Input.mousePosition;
                    Vector2 uiPos = m_world.m_levelBack.GlobalToLocal(new Vector2(mousePos.x, Screen.height - mousePos.y));
                    // 设置组件中心对齐鼠标（可选偏移）
                    sender.SetXY(uiPos.x, uiPos.y);
                }
            }
        }
        void MapClick(EventContext evt)
        {
            if (isEdit)
            {
                //Debug.Log("编辑模式");
                if (isLink)
                {
                    //Debug.Log("链接");
                    var sender = evt.sender as GComponent;
                    var pos3 = sender.position;
                    Vector2 pos = new Vector2(pos3.x, pos3.y);
                    if (linkset.Contains(pos))
                    {
                        linkset.Remove(pos);
                        sender.GetController("isLinkItem").selectedIndex = 0;
                        //Debug.Log("取消连线");
                        Debug.Log(sender);
                    }
                    else if (linkset.Count < 2)
                    { 
                        linkset.Add(pos);
                        //Debug.Log("添加连线");
                        //Debug.Log(sender);
                        sender.GetController("isLinkItem").selectedIndex = 1;
                    }
                }
                //if (isMove) Debug.Log("移动");
            }
            else
            {
                doBattle(evt);
            }
        }

        async void doBattle(EventContext evt)
        {
            var sender = evt.sender as GObject;
            var battleLevel = sender.data as string;

            var pos = sender.LocalToGlobal(Vector2.zero);
            if (pos.x < 100)
            {
                m_world.scrollPane.SetPosX((100f - sender.x), true);
            }
            if (pos.x > 920)
            {
                m_world.scrollPane.SetPosX(sender.x - 920f, true);
                //m_world.scrollPane.ScrollToView(sender);
            }

            var mapInfo = Database.Instance.GetMap(mapPackageName, battleLevel);
            m_levelInfo.SetInfo(mapPackageName, battleLevel);
            m_showLevelInfo.selectedIndex = 1;

            var teamIndex = -1;
            while (teamIndex < 0)
            {
                goTcs = new TaskCompletionSource<bool>();
                var ifGo = await goTcs.Task;
                if (ifGo)
                {
                    if (mapInfo.Contracts != null && mapInfo.Contracts.Count > 0)
                    {
                        var uiContract = UIManager.Instance.ChangeView<UI_Contract>(UI_Contract.URL);
                        uiContract.SetMap(mapPackageName, battleLevel);
                        await uiContract.WaitFinish();
                        m_showLevelInfo.selectedIndex = 0;
                        UIManager.Instance.ChangeView<GComponent>(URL);
                        return;
                    }
                    else
                    {
                        var uiTeam = UIManager.Instance.ChangeView<UI_Team>(UI_Team.URL);
                        uiTeam.IfGoBattle(true);

                        teamIndex = await uiTeam.ChooseTeam();
                        if (teamIndex < 0)
                        {
                            UIManager.Instance.ChangeView<GComponent>(URL);
                        }
                    }
                }
                else
                {
                    return;
                }
            }

            await BattleManager.Instance.StartBattle(new BattleInput()
            {
                MapName = battleLevel,
                MapPackage = mapPackageName,
                Seed = 0,
                Team = GameData.Instance.Teams[teamIndex],
                Contracts = new List<int>(contracts),
                //IsPreview = isPreview,
            }); 
            m_showLevelInfo.selectedIndex = 0;
            UIManager.Instance.ChangeView<GComponent>(URL);
        }
        void freshMaps()
        {
            foreach (var child in m_world.m_levelBack.GetChildren())
            {
                child.Dispose();
            }
            m_world.m_levelBack.RemoveChildren();
            foreach (var child in m_world.m_links.GetChildren())
            {
                child.Dispose();
            }
            m_world.m_links.RemoveChildren();
            if (Database.Instance.GetConfigPath(m_MapPackage.text) != null)
            {
                Config = JsonHelper.FromJson<Config>(SaveHelper.LoadConfig(m_MapPackage.text));
                ExcelList = Config.excels;
            }
            else
            {
                Config = new Config();
                ExcelList = new List<string>();
            }
            //Debug.Log(ExcelList[0]);
            int index = m_MapPackage.selectedIndex;
            int fileIndex = 0;
            //var maps = m_world.GetChildren().Select(x => x.data).ToArray();
            //List<string> excelNeeds = new List<string>();
            List<string> excelNames = new List<string>();
            excelNames = GameData.Instance.ExcelList.Select(x => 
                System.IO.Path.GetFileName(System.IO.Path.GetDirectoryName(x)) + "/" + System.IO.Path.GetFileName(x)
                ).ToList();
            if (index == 0)
            {
                m_world.m_IsMain.SetSelectedIndex(0);
            }
            else
            {
                m_world.m_IsMain.SetSelectedIndex(1);
            }
            m_world.m_levelBack.RemoveChildren();
            if (Database.Instance.GetConfigPath(m_MapPackage.text) != null)
            // 使用Config初始化地图
            {
                string needlist = "";
                foreach (string excel in ExcelList)
                {
                    //Debug.Log(excel);
                    if (!excelNames.Contains(excel))
                    {
                        //excelNeeds.Add(file);
                        needlist += excel + "、";
                    }
                }
                //Debug.Log(needlist);
                m_tip.text = "";
                if (needlist.Length > 0) m_tip.text = "缺少Excel文件：" + needlist;
                foreach (var file in Database.Instance.GetMaps(m_MapPackage.text))
                {
                    var battleInfo = UIPackage.CreateObject("MainUI", "BattleInfo").asCom;
                    battleInfo.text = file;
                    battleInfo.data = file;
                    //if (maps.Contains(file)) continue;
                    for (int i = 0; i < Config.lines.Count; i++)
                    {
                        GGraph line = new GGraph();
                        line.DrawPolygon(100.0f, 100.0f, Config.lines[i],Color.white);
                        m_world.m_links.AddChild(line);
                    }
                    m_world.m_levelBack.AddChild(battleInfo);
                    if (Config.positions.ContainsKey(file))
                        battleInfo.position = Config.positions[file];
                    else
                    {
                        //Debug.Log(file);
                        battleInfo.position = new Vector2((int)(fileIndex * 100) / 1000 * 200 + 238, 185 + (fileIndex * 100) % 1000f);
                        fileIndex++;
                    }
                    battleInfo.onClick.Add(MapClick);
                    battleInfo.onDragStart.Add(MapDrag);
                }
            }
            else
            // 无Config初始化地图
            {
                foreach (var file in Database.Instance.GetMaps(m_MapPackage.text))
                {
                    var battleInfo = UIPackage.CreateObject("MainUI", "BattleInfo").asCom;
                    battleInfo.text = file;
                    battleInfo.data = file;
                    m_world.m_levelBack.AddChild(battleInfo);
                    //if (maps.Contains(file)) continue;
                    battleInfo.onClick.Add(MapClick);
                    battleInfo.onDragStart.Add(MapDrag);
                    battleInfo.position = new Vector2((int)(fileIndex * 100) / 1000 * 200 + 238, 185 + (fileIndex * 100) % 1000f);
                    fileIndex++;
                }
            }
            GTreeNode rootNode = m_ExcelList.rootNode;
            //rootNode.RemoveChildren();
            for (int i = 0; i < rootNode.numChildren; i++)
            {
                //GComponent a = rootNode.GetChildAt(i).cell;
                //if (a!= null) Debug.Log(a.GetChild("title").text);
                GTreeNode folder = rootNode.GetChildAt(i);
                for (int j = 0; j < folder.numChildren; j++)
                {
                    var file = folder.GetChildAt(j);
                    if (file is GTreeNode) file.cell.Dispose();
                }
                folder.cell.Dispose();
            }
            rootNode.RemoveChildren();
            List<string> ExcelFolderPaths = Database.Instance.GetExcelPathList();
            List<string> ExcelFolderNames = new List<string>();
            List<string> ExcelFilePaths = new List<string>();
            List<string> ExcelFileNames = new List<string>();
            ExcelFolderNames.AddRange(ExcelFolderPaths.Select(x => System.IO.Path.GetFileNameWithoutExtension(x)));

            for (int i = 0; i < ExcelFolderNames.Count; i++)
            {
                GTreeNode item_folder = new GTreeNode(true);
                rootNode.AddChild(item_folder);
                //item_folder.RemoveChildren();
                //Debug.Log(item_folder.level);
                GComponent obj_folder = item_folder.cell;
                //Debug.Log(ExcelFolderNames[i]);
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
                    GTreeNode item_file = new GTreeNode(false);
                    string path = ExcelFolderNames[i] + "/" + ExcelFileNames[j] + ".xlsx";
                    rootNode.AddChild(item_file);
                    //Debug.Log(item_folder.GetChildAt(j).level);
                    GComponent obj_file = item_file.cell;
                    obj_file.GetChild("title").text = ExcelFileNames[j];
                    obj_file.GetChild("path").text = path;
                    //Debug.Log(ExcelFileNames[j]);
                    obj_file.GetChild("selectBtn").asButton.onClick.Add(() =>
                    {
                        ExccelListClicke(item_file);
                    });
                    if (ExcelList.Contains(ExcelFolderNames[i] + "/" + ExcelFileNames[j] + ".xlsx"))
                    {
                        obj_file.GetChild("selectBtn").asButton.GetController("button").selectedIndex = 1;
                    }
                    item_folder.AddChild(item_file);

                    //Debug.Log();
                    //item_file.text = ExcelFileNames[j];
                    //Debug.Log(item_file.GetChildAt(0));
                }
                ExcelFileNames.Clear();
                ExcelFilePaths.Clear();
            }
            freshNode();
        }
        public static List<Vector2> GetRectVertices(List<Vector2> points)
        {
            // 计算两点之间的中心点
            Vector2 point1 = points[0];
            Vector2 point2 = points[1];
            float centerX = (point1.x + point2.x) / 2;
            float centerY = (point1.y + point2.y) / 2;

            // 计算两点之间的角度
            float dx = point2.x - point1.x;
            float dy = point2.y - point1.y;
            float angle = (float)Math.Atan2(dy, dx);

            // 计算矩形的半宽
            float halfWidth = 5f;

            // 计算矩形的四个顶点坐标
            List<Vector2> vertices = new List<Vector2>();

            // 旋转矩阵
            float cosAngle = (float)Math.Cos(angle);
            float sinAngle = (float)Math.Sin(angle);

            // 计算四个顶点的偏移量
            Vector2 p1 = new Vector2(point1.x - halfWidth * sinAngle, point1.y + halfWidth * cosAngle);
            Vector2 p2 = new Vector2(point2.x - halfWidth * sinAngle, point2.y + halfWidth * cosAngle);
            Vector2 p3 = new Vector2(point2.x + halfWidth * sinAngle, point2.y - halfWidth * cosAngle);
            Vector2 p4 = new Vector2(point1.x + halfWidth * sinAngle, point1.y - halfWidth * cosAngle);
            vertices.Add(p1);
            vertices.Add(p3);
            vertices.Add(p2);
            vertices.Add(p4);
            //Debug.Log(p1);
            //Debug.Log(p3);
            //Debug.Log(p2);
            //Debug.Log(p4);

            return vertices;
        }

        public void Enter()
        {
            freshMaps();
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
                    //Debug.Log(path);
                }
                freshNode();
                //SaveHelper.SaveData();
                //foreach (string i in ExcelList)
                //{
                //    Debug.Log(i);
                //}
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
    }
}
