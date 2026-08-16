using MapBuilderUI;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using FairyGUI;

public class Preview : MonoBehaviour
{
    private static Preview instance;
    public bool Pause = true;
    public float time;
    public float lastTime;
    public WaveInfo waveInfo;
    UnitData enemy;
    List<PathPoint> pathinfo = new List<PathPoint>();
    GSlider slider;
    GButton playBtn;

    // @大d
    public List<GameObject> enemyList = new List<GameObject>();
    public List<EnemyMovementState> enemyStates = new List<EnemyMovementState>();

    public CountDown WaveGap = new CountDown();
    public CountDown ShowPathGap = new CountDown();
    public int wavetimes = 0;
    public bool loop = false;
    public int playspeed = 2;

    public static Preview Instance
    {
        get
        {
            if (instance == null)
            {
                GameObject gameObject = new GameObject("PreviewManager");
                DontDestroyOnLoad(gameObject);
                instance = gameObject.AddComponent<Preview>();
            }
            return instance;
        }
    }

    // 敌人移动状态类
    public class EnemyMovementState
    {
        public int currentPathIndex = 0;
        public CountDown pointDelay = new CountDown(0);
        public bool isMoving = false;
        public bool isWaiting = false;
        public bool isFinished = false;

        public void SetDelay(float delay)
        {
            pointDelay.Set(delay);
            isWaiting = delay > 0;
        }
    }

    public void Init(WaveInfo inputwaveInfo, MapInfo mapInfo, float Ltime, GSlider timeslider, GButton playbutton)
    {
        time = 0;
        lastTime = Ltime;
        Pause = true;
        slider = timeslider;
        waveInfo = inputwaveInfo;
        enemy = Database.Instance.Get<UnitData>(waveInfo.sUnitId);
        pathinfo = mapInfo.PathInfos.Find(x => x.Name == waveInfo.Path).Path;
        playBtn = playbutton;
        wavetimes = waveInfo.Count;
        WaveGap.Set(waveInfo.GapTime);
        ShowPathGap.Set(0);
    }

    public void Clear()
    {
        for (int i = 0; i < enemyList.Count; i++)
        {
            if (enemyList[i] != null)
            {
                Debug.Log("Destroy Enemy");
                Destroy(enemyList[i]);
            }
        }
        enemyList.Clear();
        enemyStates.Clear();

        time = 0;
        lastTime = 0;
        Pause = true;
        slider.value = 0;
        waveInfo = null;
        enemy = null;
        pathinfo.Clear();
        WaveGap.Set(0);
        ShowPathGap.Set(0);
        wavetimes = 0;
        loop = false;
        playspeed = 2;
    }

    private void FixedUpdate()
    {
        for (int i = 0; i < playspeed; i++)
        {
            if (waveInfo != null && pathinfo != null && pathinfo.Count > 0)
            {
                if (Pause) return;

                // 波次生成逻辑
                if (waveInfo.GapTime == 0 && waveInfo.Count != 0 && time == 0)
                {
                    for (int j = 0; j < waveInfo.Count; j++)
                    {
                        CreateEnemy();
                    }
                }
                else if (wavetimes >= 1)
                {
                    WaveGap.Update(SystemConfig.DeltaTime);
                    if (WaveGap.Finished())
                    {
                        CreateEnemy();
                        wavetimes--;
                        WaveGap.Set(waveInfo.GapTime);
                    }
                }

                // 更新所有敌人的移动
                for (int k = 0; k < enemyList.Count; k++)
                {
                    if (enemyList[k] != null)
                    {
                        UpdateEnemyMovement(k);
                    }
                }

                // 清理已完成路径的敌人
                for (int k = enemyList.Count - 1; k >= 0; k--)
                {
                    if (enemyList[k] == null || (k < enemyStates.Count && enemyStates[k].isFinished))
                    {
                        if (enemyList[k] != null)
                        {
                            Destroy(enemyList[k]);
                        }
                        enemyList.RemoveAt(k);
                        if (k < enemyStates.Count) enemyStates.RemoveAt(k);
                    }
                }

                time += SystemConfig.DeltaTime;
                slider.value = time;

                if (time > lastTime)
                {
                    if (!loop)
                    {
                        Pause = true;
                        playBtn.GetController("button").SetSelectedIndex(1);
                    }

                    // 清理所有敌人
                    for (int k = enemyList.Count - 1; k >= 0; k--)
                    {
                        if (enemyList[k] != null)
                        {
                            Destroy(enemyList[k]);
                        }
                    }
                    enemyList.Clear();
                    enemyStates.Clear();

                    WaveGap.Set(waveInfo.GapTime);
                    wavetimes = waveInfo.Count;
                    time = 0;
                    slider.value = 0;
                }
            }
        }

        if (waveInfo != null && pathinfo != null && pathinfo.Count > 0)
        {
            ShowPathGap.Update(SystemConfig.DeltaTime);
            if (ShowPathGap.Finished())
            {
                TrailManager.Instance.ShowPath(pathinfo.Select(x => x.Pos).ToList());
                ShowPathGap.Set(0.3f);
            }
        }
    }

    // 创建敌人实例
    void CreateEnemy()
    {
        if (pathinfo == null || pathinfo.Count == 0) return;

        GameObject go = CreateEnemyModel(enemy, pathinfo[0].Pos);
        if (go == null) return;

        Debug.Log("Create Enemy");
        enemyList.Add(go);

        // 初始化敌人状态
        EnemyMovementState state = new EnemyMovementState();

        // 设置第一个点的延迟
        if (pathinfo.Count > 0)
        {
            state.SetDelay(pathinfo[0].Delay);
        }

        enemyStates.Add(state);
    }

    // 更新敌人移动 - 更加健壮的版本
    void UpdateEnemyMovement(int enemyIndex)
    {
        if (enemyIndex >= enemyList.Count || enemyIndex >= enemyStates.Count) return;

        GameObject enemyObj = enemyList[enemyIndex];
        EnemyMovementState state = enemyStates[enemyIndex];

        if (enemyObj == null || state.isFinished) return;

        // 检查是否已完成路径
        if (state.currentPathIndex >= pathinfo.Count - 1)
        {
            state.isFinished = true;
            return;
        }

        // 处理当前点的延迟
        if (state.isWaiting)
        {
            state.pointDelay.Update(SystemConfig.DeltaTime);
            if (state.pointDelay.Finished())
            {
                state.isWaiting = false;

                // 如果是隐藏移动，直接跳到下一个点
                if (state.currentPathIndex < pathinfo.Count &&
                    pathinfo[state.currentPathIndex].HideMove)
                {
                    HandleHideMove(enemyIndex);
                    return;
                }
            }
            else
            {
                return; // 还在等待延迟
            }
        }

        // 正常移动逻辑
        if (state.currentPathIndex < pathinfo.Count - 1)
        {
            PathPoint currentPoint = pathinfo[state.currentPathIndex];
            PathPoint nextPoint = pathinfo[state.currentPathIndex + 1];

            // 计算移动
            Vector3 direction = (nextPoint.Pos - enemyObj.transform.position).normalized;
            float distanceToMove = enemy.Speed * 0.5f * SystemConfig.DeltaTime;

            // 计算到下一个点的距离
            float distanceToNextPoint = Vector3.Distance(enemyObj.transform.position, nextPoint.Pos);

            // 如果下一步会超过下一个点，则直接移动到下一个点
            if (distanceToMove >= distanceToNextPoint)
            {
                enemyObj.transform.position = nextPoint.Pos;

                // 更新朝向
                if (state.currentPathIndex < pathinfo.Count - 2)
                {
                    PathPoint nextNextPoint = pathinfo[state.currentPathIndex + 2];
                    float scaleX = (nextNextPoint.Pos.x - nextPoint.Pos.x) > 0 ? 1 : -1;
                    enemyObj.transform.localScale = new Vector3(scaleX, 1, 1);
                }

                // 移动到下一个点后，处理下一个点的逻辑
                state.currentPathIndex++;

                // 检查是否到达终点
                if (state.currentPathIndex >= pathinfo.Count - 1)
                {
                    state.isFinished = true;
                    return;
                }

                // 设置下一个点的延迟
                PathPoint newCurrentPoint = pathinfo[state.currentPathIndex];
                state.SetDelay(newCurrentPoint.Delay);

                // 如果是隐藏移动，直接处理
                if (newCurrentPoint.HideMove && state.isWaiting)
                {
                    HandleHideMove(enemyIndex);
                }
            }
            else
            {
                // 正常移动
                enemyObj.transform.position += direction * distanceToMove;

                // 更新朝向
                float scaleX = direction.x > 0 ? 1 : -1;
                enemyObj.transform.localScale = new Vector3(scaleX, 1, 1);
            }
        }
    }

    // 处理隐藏移动
    void HandleHideMove(int enemyIndex)
    {
        if (enemyIndex >= enemyList.Count || enemyIndex >= enemyStates.Count) return;

        GameObject enemyObj = enemyList[enemyIndex];
        EnemyMovementState state = enemyStates[enemyIndex];

        if (enemyObj == null || state.isFinished) return;

        // 直接跳到下一个点
        if (state.currentPathIndex < pathinfo.Count - 1)
        {
            enemyObj.transform.position = pathinfo[state.currentPathIndex + 1].Pos;
            state.currentPathIndex++;

            // 检查是否到达终点
            if (state.currentPathIndex >= pathinfo.Count - 1)
            {
                state.isFinished = true;
                return;
            }

            // 设置下一个点的延迟
            PathPoint newCurrentPoint = pathinfo[state.currentPathIndex];
            state.SetDelay(newCurrentPoint.Delay);

            // 如果下一个点也是隐藏移动，继续处理
            if (newCurrentPoint.HideMove && state.isWaiting)
            {
                HandleHideMove(enemyIndex);
            }
        }
    }

    public GameObject CreateEnemyModel(UnitData UnitData, Vector3 position)
    {
        if (string.IsNullOrEmpty(UnitData.Model)) return null;

        GameObject go = ResHelper.Instantiate(PathHelper.UnitPath + UnitData.Model);
        if (go != null)
        {
            go.transform.position = position;
        }
        return go;
    }

    public void PauseGame(bool isPause)
    {
        Pause = isPause;
    }
}