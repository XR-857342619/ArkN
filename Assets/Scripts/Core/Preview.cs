using MapBuilderUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using FairyGUI;
using System.Reflection;

public class Preview : MonoBehaviour
{
    private static Preview instance;
    //public Battle Battle;
    public bool Pause = true;
    //public float ExcuteTime;
    public float time;
    public float lastTime;
    public WaveInfo waveInfo;
    UnitData enemy;
    List<PathPoint> pathinfo = new List<PathPoint>();
    GSlider slider;
    GButton playBtn;
    List<GameObject> enemys = new List<GameObject>();
    List<TrailRenderer> trails = new List<TrailRenderer>();
    //public CountDown PathWaiting = new CountDown();
    public List<List<PathPoint>> tempPath = new List<List<PathPoint>>();
    public List<CountDown> countDowns = new List<CountDown>();
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
    // Start is called before the first frame update
    void Start()
    {

    }
    // Update is called once per frame
    public void Init(WaveInfo inputwaveInfo, MapInfo mapInfo, float Ltime, GSlider timeslider, GButton playbutton)
    {
        time = 0;
        lastTime = Ltime;
        Pause = true;
        slider = timeslider;
        waveInfo = inputwaveInfo;
        enemy = Database.Instance.Get<UnitData>(waveInfo.sUnitId);
        pathinfo = mapInfo.PathInfos.Find(x => x.Name == waveInfo.Path).fixedPath;
        playBtn = playbutton;
        wavetimes = waveInfo.Count;
        WaveGap.Set(0);
        ShowPathGap.Set(0);
        //playspeed = (int)speedslider.value;
    }
    public void Clear()
    {
        for (int i = 0; i < enemys.Count; i++)
        {
            Debug.Log("Destroy Enemy");
            Destroy(enemys[i], 0.1f);
        }
        time = 0;
        lastTime = 0;
        Pause = true;
        slider.value = 0;
        waveInfo = null;
        enemy = null;
        pathinfo.Clear();
        tempPath.Clear();
        countDowns.Clear();
        enemys.Clear();
        WaveGap.Set(0);
        ShowPathGap.Set(0);
        wavetimes = 0;
        //showpathtimes = 3;
        loop = false;
        playspeed = 2;
    }
    private void FixedUpdate()
    {
        for (int i = 0; i < playspeed; i++)
        {
            if (waveInfo != null)
            {
                if (Pause) return;
                if (waveInfo.GapTime == 0 && waveInfo.Count != 0 && time == 0)
                {
                    //Debug.Log(waveInfo.Count);
                    for (int j = 0; j < waveInfo.Count; j++)
                    {
                        GameObject go = CreateEnemyModel(enemy, pathinfo[j].Pos);
                        Debug.Log("Create Enemy01");
                        enemys.Add(go);
                        tempPath.Add(new List<PathPoint>());
                        //EnemyMove(go, pathinfo, enemy.Speed * 0.5f, tempPath[j]);
                    }
                }
                else if (wavetimes >= 1)
                {
                    //Debug.Log("WaveGap" + WaveGap.value);
                    WaveGap.Update(SystemConfig.DeltaTime);
                    if (WaveGap.Finished())
                    {
                        GameObject go = CreateEnemyModel(enemy, pathinfo[0].Pos);
                        Debug.Log("Create Enemy02");
                        enemys.Add(go);
                        tempPath.Add(new List<PathPoint>());
                        wavetimes--;
                        WaveGap.Set(waveInfo.GapTime);
                        //Debug.Log("wave" + wavetimes);
                        //Debug.Log(WaveGap.value);
                    }
                }
                //await Task.Delay((int)SystemConfig.DeltaTime * 1000);
                //Thread.Sleep((int)SystemConfig.DeltaTime * 1000);
                for (int k = 0; k < enemys.Count; k++)
                {
                    EnemyMove(enemys[k], pathinfo, enemy.Speed * 0.5f, k);
                    if (countDowns[k] != null && !countDowns[k].Finished())
                        countDowns[k].Update(SystemConfig.DeltaTime);
                }
                //for (int k = 0; k < trails.Count; k++)
                //{
                //    ShowPath(trails[k], pathinfo, k);
                //}
                time += SystemConfig.DeltaTime;
                slider.value = time;
                if (time > lastTime)
                {
                    if (!loop)
                    {
                        Pause = true;
                        playBtn.GetController("button").SetSelectedIndex(1);
                    }
                    //waveInfo = null;
                    WaveGap.Set(0);
                    tempPath.Clear();
                    enemys.Clear();
                    wavetimes = waveInfo.Count;
                    time = 0;
                    slider.value = 0;
                }
            }
        }
        if (waveInfo != null)
        {
            ShowPathGap.Update(SystemConfig.DeltaTime);
            if (ShowPathGap.Finished())
            {
                TrailManager.Instance.ShowPath(pathinfo.Select(x => x.Pos).ToList());
                ShowPathGap.Set(0.3f);
            }
        }
    }
    public void EnemyMove(GameObject go, List<PathPoint> pathinfo, float speed, int index)
    {
        if (go != null)
        {
            //Debug.Log(pathinfo.Count);
            //Debug.Log("Moveing");
            PathPoint now = new PathPoint();
            if (tempPath[index].Count == 0)
            {
                //Debug.Log("Strat");
                now = pathinfo[0];
                //tempPath[index].Add(now);
                countDowns.Add(new CountDown(now.Delay));
            }
            else if (tempPath[index].Count == pathinfo.Count-1)
            {
                Debug.Log("End");
                now = pathinfo[tempPath[index].Count - 1];
                countDowns.Add(new CountDown(now.Delay));
            }
            else
            {
                //Debug.Log("it's" + tempPath[index].Count);
                now = pathinfo[tempPath[index].Count - 1];
                //tempPath[index].Add(now);
            }
            if (countDowns[index].Finished())
            {
                if (now.HideMove)
                {
                    Debug.Log("Hide Move");
                    tempPath[index].Add(now);
                    go.transform.position = now.nextPoint.Pos;
                }
                else
                {
                    //Debug.Log("Move");
                    var target = go.transform.position + (now.nextPoint.Pos - now.Pos).normalized * speed * SystemConfig.DeltaTime;
                    //Debug.Log(target);
                    float ScaleX = (now.nextPoint.Pos.x - now.Pos.x) > 0 ? 1 : -1;
                    go.transform.position = target;
                    go.transform.localScale = new Vector3(ScaleX, 1, 1);
                    if (Vector3.Distance(go.transform.position, now.nextPoint.Pos) < speed * SystemConfig.DeltaTime)
                    {
                        //Debug.Log("Arrive" + tempPath[index].Count);
                        if (tempPath[index].Count == pathinfo.Count-1)
                        {
                            //if (loop)
                            //{
                            //go.transform.position = pathinfo[0].Pos;
                            //countDowns[index].Set(0);
                            //countDowns.Add(new CountDown(pathinfo[0].Delay));
                            //}
                            //else
                            //{

                            go.transform.position = pathinfo[pathinfo.Count - 1].Pos;
                            Destroy(enemys[index], 0);
                            //}
                        }
                        else
                        {
                            go.transform.position = now.nextPoint.Pos;
                            tempPath[index].Add(now);
                            countDowns.Add(new CountDown(now.nextPoint.Delay));
                        }
                    }
                }
            }
        }
        #region

        //float lastdistance = -10000000;
        //for (int i = 0; i < pathinfo.Count; i++)
        //{
        //    float distance = Vector3.Distance(go.transform.position, pathinfo[i].Pos);
        //    if (distance < speed * SystemConfig.DeltaTime)
        //    {
        //        go.transform.position = pathinfo[i].Pos;
        //        //now = pathinfo[i];
        //        //lastdistance = distance;
        //        break;
        //    }
        //}
        //if (!PathWaiting.Finished())
        //{
        //    if (AnimationName == GetMoveAnimation()) SetStatus(StateEnum.Idle);
        //    PathWaiting.Update(SystemConfig.DeltaTime + WaitTimeEx);
        //    return;
        //}
        //CheckArrive();

        //var delta = TempTarget - Position;
        //if (delta != Vector3.zero) Direction = new Vector2(delta.x, delta.z);
        //float scaleX = TargetScaleX;
        //if (delta.x > 0) scaleX = 1;
        //else if (delta.x < 0) scaleX = -1;
        //else
        //{
        //    bool success = false;
        //    for (int i = NowPathPoint + 1; i < PathPoints.Count; i++)
        //    {
        //        var x = GetPoint(i).x;
        //        if (x != Position.x)
        //        {
        //            scaleX = Math.Sign(x - Position.x);
        //            success = true;
        //        }
        //    }
        //    if (!success)
        //        scaleX = TargetScaleX;
        //}
        //TargetScaleX = scaleX;
        //if ((TempTarget - Position).magnitude < Speed * SystemConfig.DeltaTime)
        //{
        //    //Debug.Log("Arrive");
        //    Position = TempTarget;
        //    //抵达临时目标
        //    TempIndex++;
        //    if (TempIndex >= TempPath.Count - 1)
        //    {
        //        NowPathPoint++;

        //        if (NowPathPoint == PathPoints.Count - 1)
        //        {
        //            //破门了
        //            //Battle.DoDamage(UnitData.Damage);
        //            Finish(false);
        //        }
        //        else
        //        {
        //            PathWaiting.Set(PathPoints[NowPathPoint].Delay);
        //            //往下个点走
        //            TempPath = null;
        //        }
        //    }
        //}
        //else
        //{
        //    var target = Position + (TempTarget - Position).normalized * Speed * SystemConfig.DeltaTime;
        //    Position = target;
        //    //Debug.Log(Position.x);
        //}
        #endregion
    }
        public GameObject CreateEnemyModel(UnitData UnitData, Vector3 position)
    {
        if (string.IsNullOrEmpty(UnitData.Model)) return null;
        //Debug.Log(UnitData.Model);
        GameObject go = ResHelper.Instantiate(PathHelper.UnitPath + UnitData.Model);
        //if (go == null)
        //{
        //    Debug.Log(PathHelper.UnitPath + UnitData.Model);
        //    Debug.Log(UnitData.Model + " not found");
        //}
        //UnitModel = go.GetComponent<UnitModel>();
        //UnitModel.Init(this);
        go.transform.position = position;
        return go;
    }
    public void PauseGame(bool isPause)
    {
        Pause = isPause;
    }
}
