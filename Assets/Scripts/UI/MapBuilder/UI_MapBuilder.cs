using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using FairyGUI;

namespace MapBuilderUI
{
    partial class UI_MapBuilder : IGameUIView
    {
        public static UI_MapBuilder Instance;
        public MapInfo MapInfo;
        public string scene;

        partial void Init()
        {
            Instance = this;
            m_StartPage.m_MapPackageIndex.onChanged.Add(() =>
            {
                m_StartPage.m_quickLoad.items = m_StartPage.m_quickLoad.values = Database.Instance.GetMaps(m_StartPage.m_MapPackageIndex.value).ToArray();
            });
            m_exit.onClick.Add(() =>
            {
                UIManager.Instance.ChangeView<MainUI.UI_Main>(MainUI.UI_Main.URL);
                MapManager.Instance?.ShowPath(null);
                if (m_state.selectedIndex != 0)
                {
                    // === 修复场景卸载泄漏：原逻辑只卸 scene / "MapBuilder" 其中一个，
                    // 另一个连同它的方向光永远留在内存里；下次进图再叠一份光 → 越进越亮（过曝），
                    // 且本次 Play 会话内不可恢复。现在退出时把两者都卸干净。 ===
                    if (!string.IsNullOrEmpty(scene) && SceneManager.GetSceneByName(scene).isLoaded)
                        SceneManager.UnloadSceneAsync(scene);
                    if (SceneManager.GetSceneByName("MapBuilder").isLoaded)
                        SceneManager.UnloadSceneAsync("MapBuilder");
                }
                m_MidPage.Despawn();
            });
            m_back.onClick.Add(goMain);
            //m_DoPath.onClick.Add(() => { m_state.selectedIndex = 3; AstarPath.active.Scan(); m_PathPage.Fresh(); m_PathPage.FreshPoints(); });
            m_DoPath.onClick.Add(() => { m_state.selectedIndex = 3; m_PathPage.Fresh(); m_PathPage.FreshPoints(); });
            //m_DoUnit.onClick.Add(() => { m_state.selectedIndex = 4; AstarPath.active.Scan(); m_WavePage.Fresh(); });
            m_DoUnit.onClick.Add(() => { m_state.selectedIndex = 4; m_WavePage.Fresh(); });
            m_DoMidUnit.onClick.Add(() => { m_state.selectedIndex = 5; m_MidPage.Fresh(); });

            m_upgrid.onClick.Add(() => { m_state.selectedIndex = 6; MapManager.Instance.Brush(upper); });
            m_downgrid.onClick.Add(() => { m_state.selectedIndex = 6; MapManager.Instance.Brush(lower); });
            m_moveable.onClick.Add(() => { m_state.selectedIndex = 6; MapManager.Instance.Brush(canBuild); });
            m_unmoveable.onClick.Add(() => { m_state.selectedIndex = 6; MapManager.Instance.Brush(cantBuild); });

            m_X.onChanged.Add(setCamera);
            m_Y.onChanged.Add(setCamera);
            m_Z.onChanged.Add(setCamera);

            m_StartPage.m_load.onClick.Add(() =>
            {
                var info = Database.Instance.GetMap(m_StartPage.m_SMapPackageIndex.text, m_StartPage.m_FileName.text);
                if (info != null)
                {
                    m_MidPage.Despawn();
                    MapInfo = info;
                    if (info.PathInfos == null) info.PathInfos = new List<PathInfo>();
                    if (info.UnitInfos == null) info.UnitInfos = new List<UnitInfo>();
                    if (info.WaveInfos == null) info.WaveInfos = new List<WaveInfo>();
                    else
                    {
                        foreach (var waveInfo in info.WaveInfos)
                        {
                            if (waveInfo.UnitId == null || waveInfo.sUnitId != null) continue;
                            waveInfo.sUnitId = Database.Instance.Get<UnitData>(waveInfo.UnitId.Value).Id;
                        }
                    }
                    if (info.UnitOvDatas == null) info.UnitOvDatas = new List<OverwriteUnitInfo>();
                    if (info.Contracts == null) info.Contracts = new List<string>();

                    //m_StartPage.m_SceneName.text = info.Scene;
                    m_StartPage.m_width.text = info.GridInfos.GetLength(0).ToString();
                    m_StartPage.m_height.text = info.GridInfos.GetLength(1).ToString();
                    m_StartPage.Fresh();
                }
            });
            m_StartPage.m_next.onClick.Add(async () =>
            {
                //scene = m_StartPage.m_SceneName.text;
                //if (string.IsNullOrEmpty(scene))
                //{
                    if (MapInfo.GridInfos == null)
                    {
                        MapInfo.GridInfos = new GridInfo[int.Parse(m_StartPage.m_width.text), int.Parse(m_StartPage.m_height.text)];
                        MapInfo.CameraPos = new Vector3((MapInfo.GridInfos.GetLength(0) - 1) / 2f, 0.7f * MapInfo.GridInfos.GetLength(0), -4.5f + (MapInfo.GridInfos.GetLength(1) - 1) / 3f);

                        for (int i = 0; i < MapInfo.GridInfos.GetLength(0); i++)
                        {
                            for (int j = 0; j < MapInfo.GridInfos.GetLength(1); j++)
                            {
                                MapInfo.GridInfos[i, j] = new GridInfo() { X = i, Y = j, CanBuildUnit = true, CanMove = true, FarAttack = false };
                            }
                        }
                    }
                    await SceneManager.LoadSceneAsync("MapBuilder", LoadSceneMode.Additive);
                    SceneManager.SetActiveScene(SceneManager.GetSceneByName("MapBuilder"));
                    changeCamera();
                    MapManager.Instance.Build(MapInfo.GridInfos);
                    goMain();
                // === 2026-09-26 修复 68 连错：作者移除了 FUI 的 SceneName 输入框（MapBuilderUI_fui.bytes
                // 同步更新，m_SceneName 绑定已不存在），只注释了上面 if 头三行，没注释配对的 else 块
                // → 孤儿大括号 + 无 if 的 else，语法崩塌级联报错。此处补齐注释：场景直载路
                // （else 分支，含 GridSkinData 皮肤采集补丁）随该功能一并禁用，原代码留档如下。 ===
                /*
                }
                else
                {
                    MapInfo.Scene = scene;
                    await SceneManager.LoadSceneAsync(scene, LoadSceneMode.Additive);
                    var grids = MapManager.Instance.gameObject.GetComponentsInChildren<MapGrid>();
                    MapInfo.GridInfos = new GridInfo[grids.Max(x => x.X + 1), grids.Max(x => x.Y + 1)];
                    foreach (var g in grids)
                    {
                        // === 皮肤笔刷补丁：读取场景格子上已保存的皮肤数据，进战斗时可还原 ===
                        var skinData = g.GetComponent<GridSkinData>();
                        MapInfo.GridInfos[g.X, g.Y] = new GridInfo()
                        {
                            X = g.X,
                            Y = g.Y,
                            CanBuildUnit = g.CanBuildUnit,
                            FarAttack = g.FarAttackGrid,
                            CanMove = g.CanMove,
                            Skin = skinData != null && !string.IsNullOrEmpty(skinData.Data.SkinName)
                                ? skinData.Data.Clone()
                                : null,
                            // 组合模式：一并保存叠加层
                            SkinExtras = skinData != null && skinData.ExtraSkins.Count > 0
                                ? skinData.ExtraSkins.Where(x => x != null).Select(x => x.Clone()).ToList()
                                : null,
                        };
                    }
                    goMain();
                }
                */
                m_MidPage.UpdatePoints();
            });
            m_yes.onClick.Add(() => ovTcs.SetResult(true));
            m_no.onClick.Add(() => ovTcs.SetResult(false));

            m_save.onClick.Add(async () =>
            {
                try
                {
                    check();
                    // === 皮肤笔刷补丁：保存前强制把场景皮肤数据同步进 GridInfos ===
                    // （不依赖笔刷窗口的事件同步，保证"改完即存"一定落盘）
                    if (MapInfo != null && MapInfo.GridInfos != null)
                    {
                        var mm = MapManager.Instance;
                        if (mm != null)
                            GridSkinData.SyncSkinsToGridInfos(MapInfo.GridInfos, mm.GetComponentsInChildren<MapGrid>());
                    }
                    if (SaveHelper.ExistFile(m_StartPage.m_SMapPackageIndex.text, "/Map/" + m_StartPage.m_SMapPackageIndex.text + "/" + m_StartPage.m_FileName.text))
                    {
                        var result = await ShowCancelWindow();
                        if (!result)
                        {
                            Debug.LogWarning($"[MapBuilder] 已取消覆盖「{m_StartPage.m_FileName.text}」，本次未保存");
                            return;
                        }
                    }
                    var json = JsonHelper.ToJson(MapInfo);
                    SaveHelper.Save(json, m_StartPage.m_SMapPackageIndex.text, "/Map/" + m_StartPage.m_SMapPackageIndex.text + "/" + m_StartPage.m_FileName.text);
                    Database.Instance.Maps.Remove(m_StartPage.m_SMapPackageIndex.text+ ":" +m_StartPage.m_FileName.text);
                    Database.Instance.Maps.Add(m_StartPage.m_SMapPackageIndex.text + ":" + m_StartPage.m_FileName.text, MapInfo);
                    m_saveSuccess.Play();
                    Debug.Log($"[MapBuilder] 地图已保存：{m_StartPage.m_SMapPackageIndex.text}/{m_StartPage.m_FileName.text}.txt（{json.Length / 1024} KB）");
                }
                catch (Exception e)
                {
                    // async 回调异常不会弹窗，必须显式报出来：此前 check() 越界/文件被占用都表现为"点了保存没反应"
                    Debug.LogError($"[MapBuilder] 保存失败：{e.GetType().Name}: {e.Message}\n{e.StackTrace}");
                }
            });
            m_StartPage.m_quickLoad.onChanged.Add(() =>
            {
                m_StartPage.m_FileName.text = m_StartPage.m_quickLoad.text;
                m_StartPage.m_load.onClick.Call();
            });

            // === 皮肤笔刷·运行时版：整合进游戏内造图界面（按 T 显隐、面板可拖动）。
            // 纯代码构建的 FairyGUI 面板挂在本视图下，不改 MapBuilderUI_fui.bytes；
            // 保存链路复用 m_save 的强制 SyncSkinsToGridInfos，无需额外同步。 ===
            SkinBrushRuntime.Install(this);
        }

        TaskCompletionSource<bool> ovTcs;
        async Task<bool> ShowCancelWindow()
        {
            m_sure2.selectedIndex = 1;
            m_mapName2.text = m_StartPage.m_FileName.text;
            ovTcs = new TaskCompletionSource<bool>();
            var result = await ovTcs.Task;
            m_sure2.selectedIndex = 0;
            return result;
        }

        void changeCamera()
        {
            Camera.main.transform.position = MapInfo.CameraPos;
            m_X.text = MapInfo.CameraPos.x.ToString();
            m_Y.text = MapInfo.CameraPos.y.ToString();
            m_Z.text = MapInfo.CameraPos.z.ToString();
        }

        protected override void OnUpdate()
        {
            base.OnUpdate();
            if (m_state.selectedIndex == 2)
            {
                Vector3 v3 = new Vector3();
                v3.y = -Input.GetAxis("Mouse ScrollWheel") * 50;
                if (Input.GetKey(KeyCode.Mouse2))
                {
                    v3.x = -Input.GetAxis("Mouse X") * 5;
                    v3.z = -Input.GetAxis("Mouse Y") * 5;
                }
                MapInfo.CameraPos += v3 * Time.deltaTime;
                changeCamera();
            }
        }

        public void Enter()
        {
            scene = "";
            MapInfo = new MapInfo()
            {
                Contracts = new List<string>(),
                Description = "挑战xxxx",
                MapName = "地图名",
                InitCost = 10,
                InitHp = 3,
                MaxBuildCount = 9,
                MaxCost = 99,
                PathInfos = new List<PathInfo>(),
                UnitInfos = new List<UnitInfo>(),
                WaveInfos = new List<WaveInfo>(),
                UnitOvDatas = new List<OverwriteUnitInfo>(),
            };
            m_state.selectedIndex = 0;
            m_StartPage.Fresh();
            m_StartPage.m_SMapPackageIndex.items = m_StartPage.m_SMapPackageIndex.values = Database.Instance.GetMapPackages().ToArray();
            m_StartPage.m_MapPackageIndex.items = m_StartPage.m_MapPackageIndex.values = Database.Instance.GetMapPackages().ToArray();
            m_StartPage.m_quickLoad.items = m_StartPage.m_quickLoad.values = Database.Instance.GetMaps("Main").ToArray();
        }

        void check()
        {
            foreach (var waveInfo in MapInfo.WaveInfos)
            {
                if (!MapInfo.PathInfos.Any(x=>x.Name== waveInfo.Path))
                {
                    // 空路径表时取 [0] 会越界 → 整个保存流程在写盘前崩掉（表现为"点保存无效"）
                    if (MapInfo.PathInfos.Count == 0)
                    {
                        Debug.LogWarning("[MapBuilder] check 跳过：地图有波次但没有任何路径，波次路径引用未修正");
                        break;
                    }
                    waveInfo.Path = MapInfo.PathInfos[0].Name;
                }
            }
        }

        void goMain()
        {
            if (m_state.selectedIndex == 6) MapManager.Instance.EndBrush();
            // === v3.5 皮肤防复活：撤销/笔刷窗口关闭等场景下 OnUndoRedo 的 MapInfo 重同步
            // 可能没跑（undoRedoPerformed 订阅随窗口 OnDisable 注销），MapInfo 里残留已撤销
            // 的皮肤 → 不保存直接进战斗时 ApplySkins 照残留数据重建 = "撤销物显现"。
            // 离开造图页前以场景为唯一真值强制重同步一次。 ===
            if (Application.isPlaying && MapInfo != null && MapInfo.GridInfos != null)
            {
                var mmSync = MapManager.Instance;
                if (mmSync != null)
                    GridSkinData.SyncSkinsToGridInfos(MapInfo.GridInfos, mmSync.GetComponentsInChildren<MapGrid>());
            }
            m_PathPage.NowPoints = null;
            m_PathPage.NowSelect = null;
            if (string.IsNullOrEmpty(scene))
            {
                m_state.selectedIndex = 2;
            }
            else
            {
                m_state.selectedIndex = 1;
            }
        }

        void upper(MapGrid mapGrid)
        {
            if (!mapGrid.FarAttackGrid)
            {
                mapGrid.FarAttackGrid = true;
                MapInfo.GridInfos[mapGrid.X, mapGrid.Y].FarAttack = true;
                mapGrid.AutoBuild();
            }
        }
        void lower(MapGrid mapGrid)
        {
            if (mapGrid.FarAttackGrid)
            {
                mapGrid.FarAttackGrid = false;
                MapInfo.GridInfos[mapGrid.X, mapGrid.Y].FarAttack = false;
                mapGrid.AutoBuild();
            }
        }
        void cantBuild(MapGrid mapGrid)
        {
            if (mapGrid.CanBuildUnit)
            {
                mapGrid.CanBuildUnit = false;
                MapInfo.GridInfos[mapGrid.X, mapGrid.Y].CanBuildUnit = false;
                mapGrid.AutoBuild();
            }
        }
        void canBuild(MapGrid mapGrid)
        {
            if (!mapGrid.CanBuildUnit)
            {
                mapGrid.CanBuildUnit = true;
                MapInfo.GridInfos[mapGrid.X, mapGrid.Y].CanBuildUnit = true;
                mapGrid.AutoBuild();
            }
        }

        void setCamera()
        {           
            Camera.main.transform.position = new Vector3(float.Parse(m_X.text), float.Parse(m_Y.text), float.Parse(m_Z.text));
            MapInfo.CameraPos = Camera.main.transform.position;
        }
    }
}
