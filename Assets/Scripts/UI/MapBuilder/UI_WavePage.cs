using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using FairyGUI;
using UnityEngine;

namespace MapBuilderUI
{
    partial class UI_WavePage
    {
        public MapInfo MapInfo => (parent as UI_MapBuilder).MapInfo;
        public WaveInfo NowSelect;
        public PreviewInfos PreviewInfos = new PreviewInfos();
        string[] DropInfo;
        TaskCompletionSource<int> tcs;
        bool existOnly;
        bool midOnly;
        float lastTime = 0;
        public Map Map = new Map();
        bool pause = false;
        Preview preview = Preview.Instance;
        #region
        //public List<(Vector3, float)> pathtimepoints = new List<(Vector3, float)>();
        //public Dictionary<string, float> pathtimedict = new Dictionary<string, float>();
        //public Dictionary<string, Dictionary<float, PathPoint>> time_pathpoints_dict = new Dictionary<string, Dictionary<float, PathPoint>>();
        //public Dictionary<string, UnitData> unitdata_dict = new Dictionary<string, UnitData>();
        #endregion
        partial void Init()
        {
            m_selectBack.onClick.Add(() => { tcs.TrySetCanceled(); m_selectEnemy.selectedIndex = 0; });
            m_filterName.onFocusOut.Add(filterList);
            m_AddWave.onClick.Add(() =>
            {
                var info = new WaveInfo()
                {
                    Path = DropInfo.Length > 0 ? DropInfo[0] : "",
                    Count = 1,
                };
                MapInfo.WaveInfos.Add(info);
                Fresh();
            });
            m_CopyWave.onClick.Add(() =>
            {
                int index = MapInfo.WaveInfos.IndexOf(NowSelect);
                WaveInfo info;
                if (NowSelect == null)
                {
                    info = new WaveInfo()
                    {
                        Path = DropInfo.Length > 0 ? DropInfo[0] : "",
                        Count = 1,
                    };
                }
                else
                {
                    info = JsonHelper.Clone<WaveInfo>(NowSelect);
                }
                if (index == -1)
                {
                    MapInfo.WaveInfos.Add(info);
                }
                else
                {
                    MapInfo.WaveInfos.Insert(index + 1, info);
                }
                Fresh();
            });
            m_DeleteWave.onClick.Add(() =>
            {
                if (NowSelect == null) return;
                MapInfo.WaveInfos.Remove(NowSelect);
                Fresh();
                FreshPath();
            });
            m_wavwList.onClickItem.Add((x) =>
            {
                var pathUI = x.data as UI_WaveInfo;
                NowSelect = pathUI.WaveInfo;
                m_PreviewBtn.selectedIndex = 1;
                FreshPath();
            });
            m_filterList.onClickItem.Add((x) =>
            {
                var enemyUI = x.data as UI_EnemyInfo;
                tcs.TrySetResult(enemyUI.UnitData == null ? -1 : Database.Instance.GetIndex<UnitData>(enemyUI.UnitData));
            });
            m_Hide_2.onClick.Add(() =>
            {
                m_Hide.selectedIndex = m_Hide.selectedIndex == 0 ? 1 : 0;
            });
            m_filterList.SetVirtual();
            m_filterList.itemRenderer = filterRender;
            m_ExistOnly.selected = true;
            m_ExistOnly.onClick.Add(() =>
            {
                existOnly = !existOnly;
                filterList();
            });
            m_MidOnly.onClick.Add(() =>
            {
                midOnly = !midOnly;
                filterList();
            });
            m_perview.onClick.Add(freshPreview);
            m_playBtn.onClick.Add(StartWave);
            m_playSpeed.value = 2;
            m_playSpeed.onChanged.Add(() => preview.playspeed = (int)m_playSpeed.value);
            m_loopBtn01.onClick.Add(() => 
            {
                if (m_loopBtn01.m_mode.selectedIndex != 2)
                    m_loopBtn01.m_mode.selectedIndex += 1;
                else
                    m_loopBtn01.m_mode.selectedIndex = 0;
                if (m_loopBtn01.m_mode.selectedIndex == 0)
                    preview.loop = false;
                else
                    preview.loop = true;
            });
        }

        public void Fresh()
        {
            DropInfo = MapInfo.PathInfos.Select(x => x.Name).ToArray();
            m_wavwList.RemoveChildrenToPool();
            foreach (var waveInfo in MapInfo.WaveInfos)
            {
                var waveInfoUI = m_wavwList.AddItemFromPool() as UI_WaveInfo;
                waveInfoUI.SetInfo(waveInfo, DropInfo);
                waveInfoUI.selected = NowSelect == waveInfo;
            }
            FreshPath();
        }

        void FreshPath()
        {
            if (NowSelect != null && !string.IsNullOrEmpty(NowSelect.Path))
            {
                var p = MapInfo.PathInfos.Find(x => x.Name == NowSelect.Path);
                if (p != null)
                    MapManager.Instance.ShowPath(p.Path, p.FlyPath);
                else
                    MapManager.Instance.ShowPath(null);
            }
            else
                MapManager.Instance.ShowPath(null);
        }
        public async Task<int> Choose()
        {
            m_selectEnemy.selectedIndex = 1;
            filterList();
            tcs = new TaskCompletionSource<int>();
            var result= await tcs.Task;
            m_selectEnemy.selectedIndex = 0;
            return result;
        }

        List<UnitData> filters = new List<UnitData>();
        void filterList()
        {
            List<UnitData> unitList = Database.Instance.GetAll<UnitData>().ToList();
            for (int i = 0; i < unitList.Count; i++)
            {
                if (unitList[i] == null)
                {
                    Debug.Log("unitList[" + i + "] is null");
                    unitList.RemoveAt(i);
                }
            }
            filters = unitList.Where(
                x => existOnly && !midOnly ? MapInfo.WaveInfos.Any(y => y.sUnitId == x.Id) : true
                ).Where(x => midOnly ? x.Type == "中立单位" : x.Type == "敌人").Where(
                x => x.Name == null|| x.Name.Contains(m_filterName.text)).ToList();
            filters.Insert(0, null);
            m_filterList.numItems = filters.Count;
            //m_filterList.RemoveChildrenToPool();
            //(m_filterList.AddItemFromPool() as UI_EnemyInfo).SetInfo(null);
            //foreach (var unitData in list)
            //{
            //    var enemyInfoUI = m_filterList.AddItemFromPool() as UI_EnemyInfo;
            //    enemyInfoUI.SetInfo(unitData);
            //}
        }

        void filterRender(int index,GObject item)
        {
            var enemyInfoUI = item as UI_EnemyInfo;
            enemyInfoUI.SetInfo(filters[index]);
        }
        void freshPreview()
        {
            if (m_Preview.selectedIndex == 1)
            {
                #region
                //foreach (var waveInfo in MapInfo.WaveInfos)
                //{
                //    float waveTime = 0;
                //    waveTime += waveInfo.Delay;
                //    //List<PathPoint> tmpPoints = new List<PathPoint>();
                //    UnitData unit = Database.Instance.Get<UnitData>(waveInfo.sUnitId);
                //    unitdata_dict[waveInfo.sUnitId] = unit;
                //    float speed = unit.Speed * 0.5f;
                //    PathInfo path = MapInfo.PathInfos.Find(x => x.Name == waveInfo.Path);
                //    if (!pathtimedict.ContainsKey(path.Name + speed.ToString()))
                //    {
                //        float tmpT = path.length / speed;
                //        //Debug.Log(waveInfo.sUnitId);
                //        //PreviewInfos.Points.Add(tmpPoints);
                //        Dictionary<float, PathPoint> tmpDict = new Dictionary<float, PathPoint>();
                //        tmpDict[0] = path.fixedPath[0];
                //        for (int i = 0; i < path.distances.Count; i++)
                //        {
                //            float distance = path.distances[i];
                //            tmpDict[distance / speed] = path.fixedPath[i+1];
                //        }
                //        time_pathpoints_dict[path.Name + speed.ToString()] = tmpDict;
                //        pathtimedict[path.Name + speed.ToString()] = tmpT;
                //        waveTime += tmpT;
                //    }
                //    else waveTime += pathtimedict[path.Name + speed.ToString()];
                //    waveTime += path.time;
                //    waveTime += waveInfo.Count * waveInfo.GapTime;
                //    if (waveTime > lastTime) lastTime = waveTime;
                //    PreviewInfos.TimePoints.Add((waveInfo.Delay, waveTime));
                //}
                #endregion
                if (NowSelect.sUnitId != null)
                {
                    float speed = Database.Instance.Get<UnitData>(NowSelect.sUnitId).Speed * 0.5f;
                    //Debug.Log(speed);
                    PathInfo pathinfo = MapInfo.PathInfos.Find(x => x.Name == NowSelect.Path);
                    UI_PathPage ui_PathPage = new UI_PathPage();
                    ui_PathPage.setFixedPathPoint(pathinfo);
                    if (NowSelect.Count > 1)
                        lastTime = pathinfo.length / speed + NowSelect.GapTime * (NowSelect.Count - 1);
                    else
                        lastTime = pathinfo.length / speed;

                    m_progressBar.max = lastTime;
                    m_progressBar.value = 0;
                    //Debug.Log(lastTime);
                    preview.Init(NowSelect, MapInfo, lastTime, m_progressBar, m_playBtn);
                    //m_progressBar.value = preview.time;
                }
            }
            else
            {
                FreshPath();
                preview.Clear();
                m_playBtn.GetController("button").selectedIndex = 1;
                m_playSpeed.value = 2;
                m_loopBtn01.m_mode.selectedIndex = 0;
            }
            
        }
        public async void StartWave()
        {
            #region
            //float nowTime = (float)m_progressBar.value;
            //int wavewIndex = 0;
            ////List<int> wavewIndexs = new List<int>();
            //for (int i =0; i < PreviewInfos.TimePoints.Count; i++)
            //{
            //    var timepoint = PreviewInfos.TimePoints[i];
            //    if (timepoint.Item1 <= nowTime && timepoint.Item2 > nowTime)
            //    {
            //        wavewIndex = i;
            //        break;
            //    }
            //}
            ////for (int i = wavewIndex+1; i < PreviewInfos.TimePoints.Count; i++)
            ////{
            ////    var timepoint = PreviewInfos.TimePoints[i];
            ////    if (timepoint.Item1 < PreviewInfos.TimePoints[wavewIndex].Item2 && timepoint.Item1 > nowTime) wavewIndexs.Add(i);
            ////}
            //for (float time = nowTime; time <= lastTime; time += SystemConfig.DeltaTime)
            //{
            //    if (time == lastTime)
            //    {
            //        m_progressBar.value = 0;
            //        break;
            //    }
            //    for (int i = wavewIndex; i < MapInfo.WaveInfos.Count; i++)
            //    {
            //        if (MapInfo.WaveInfos[i].CheckPoint != 0) continue;
            //        if (PreviewInfos.TimePoints[i].Item1 <= time)
            //        {
            //            WaveInfo waveInfo = MapInfo.WaveInfos[i];
            //            UnitData enemy = Database.Instance.Get<UnitData>(waveInfo.sUnitId);
            //            float speed = unitdata_dict[waveInfo.sUnitId].Speed * 0.5f;
            //            Dictionary<float, PathPoint> pathpoints = time_pathpoints_dict[waveInfo.Path + speed.ToString()];
            //            PathInfo pathinfo = MapInfo.PathInfos.Find(x => x.Name == waveInfo.Path);
            //            for (int j = 0; j < waveInfo.Count; j++)
            //            {
            //                //float tmptime = waveInfo.Count * waveInfo.GapTime;
            //                //float timediffence = nowTime - PreviewInfos.TimePoints[wavewIndex].Item1;
            //                // 根据时间差定位到当前路径点
            //                int index = pathinfo.fixedPath.FindIndex(pathpoints[time]);
            //                var angle = Vector3.Angle();
            //                Vector3 position = Vector3.zero;
            //                GameObject go = CreateEnemyModel(enemy, position);
            //            }
            //        }
            //    }
            //}
            #endregion
            MapManager.Instance.ShowPath(null);
            preview.PauseGame(pause);
            pause = !pause;
        }
    }
}
