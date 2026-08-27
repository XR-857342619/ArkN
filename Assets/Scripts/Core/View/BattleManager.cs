using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

public class BattleManager : MonoBehaviour
{
    public static BattleManager Instance
    {
        get
        {
            if (instance == null)
            {
                GameObject gameObject = new GameObject("BattleManager");
                DontDestroyOnLoad(gameObject);
                instance = gameObject.AddComponent<BattleManager>();
            }
            return instance;
        }
    }
    private static BattleManager instance;
    private static int battleExitCounter = 0;

    public Battle Battle;

    public bool Pause;

    public float ExcuteTime;

    public int TimeScale = 1;
    public bool IsPreview = false;
    public int RecoverPowervSpeed = 1;
    public bool IsInfCost = false;
    public bool IsInfHealth = false;
    public bool IsNoCD = false;
    public bool IsNoLimitBuild = false;
    public bool IsInfUnitCount = false;
    public bool IsShowDetails = false;

    public List<OpDamageInfo> OpDamageInfos = new List<OpDamageInfo>();


    private void Update()
    {
        if (Battle != null)
        {
            if (Pause) return;
            ExcuteTime += Time.deltaTime;
            int frame = Mathf.FloorToInt(ExcuteTime / SystemConfig.DeltaTime);
            for (int i = 0; i < frame - Battle.Tick; i++)
            {
                Battle.Update();
            }
        }
    }

    TaskCompletionSource<bool> battleTcs;

    public async Task StartBattle(BattleInput battleConfig)
    {
        var loadingUI = UIManager.Instance.ChangeView<MainUI.UI_Loading>(MainUI.UI_Loading.URL);
        loadingUI.m_name.text = battleConfig.MapName;
        loadingUI.SetProgress(0f, "正在准备战斗...");
        SaveHelper.SaveData();
        Pause = true;
        battleTcs = new TaskCompletionSource<bool>();
        var mapInfo = Database.Instance.GetMap(battleConfig.MapPackage, battleConfig.MapName);
        loadingUI.SetProgress(0.05f, "正在读取地图数据...");
        var sceneName = mapInfo.Scene;
        loadingUI.SetProgress(0.1f, "正在加载场景...");
        if (string.IsNullOrEmpty(sceneName))
        {
            await SceneManager.LoadSceneAsync("MapBuilder", LoadSceneMode.Additive);
            MapManager.Instance.Build(mapInfo.GridInfos);
            await TimeHelper.Instance.WaitAsync(0.1f);
            Camera.main.transform.position = mapInfo.CameraPos;
            Camera.main.transform.GetComponent<BattleCamera>().startPosition = mapInfo.CameraPos;
           sceneName = "MapBuilder";
            //AstarPath.active.Scan();
        }
        else
        {
            await SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
        }
        SceneManager.SetActiveScene(SceneManager.GetSceneByName(sceneName));
        loadingUI.SetProgress(0.25f, "场景加载完成");
        //AudioManager.Instance.PlayBackgroundAudio("battle");
        AudioManager.Instance.PlayBackgroundAudio("已至");
        await TimeHelper.Instance.WaitAsync(0.5f);
        Battle = new Battle();
        Battle.Init(battleConfig);
        loadingUI.SetProgress(0.35f, "正在初始化战斗...");
        await TimeHelper.Instance.WaitAsync(0.1f);
        //AstarPath.active.Scan();
        
        if (battleConfig.Team == null)
        {
            TipManager.Instance.ShowTip("确定不携带干员?");
        }
        else
        {
            int teamCount = battleConfig.Team.Cards.Count;
            for (int i = 0; i < teamCount; i++)
            {
                float progress = teamCount <= 0 ? 0.5f : Mathf.Lerp(0.4f, 0.65f, (float)i / teamCount);
                loadingUI.SetProgress(progress, $"正在预加载干员 ({i + 1}/{teamCount})...");

                Card card = battleConfig.Team.Cards[i];
                await ResHelper.Prepare(Database.Instance.GetIndex<UnitData>(card.UnitId), i >= battleConfig.Team.UnitSkill.Count ? -1 : battleConfig.Team.UnitSkill[i]);
            }
        }

        int waveCount = mapInfo.WaveInfos.Count;
        for (int i = 0; i < waveCount; i++)
        {
            var wave = mapInfo.WaveInfos[i];
            if (!string.IsNullOrEmpty(wave.sUnitId))
            {
                float progress = waveCount <= 0 ? 0.7f : Mathf.Lerp(0.65f, 0.85f, (float)i / waveCount);
                loadingUI.SetProgress(progress, $"正在预加载波次敌人 ({i + 1}/{waveCount})...");
                await ResHelper.Prepare(Database.Instance.GetIndex<UnitData>(wave.sUnitId));
            }
        }

        List<UnitInfo> toRemove = new List<UnitInfo>();
        int sceneUnitCount = mapInfo.UnitInfos.Count;
        for (int i = 0; i < sceneUnitCount; i++)
        {
            var wave = mapInfo.UnitInfos[i];
            try
            {
                float progress = sceneUnitCount <= 0 ? 0.9f : Mathf.Lerp(0.85f, 0.95f, (float)i / sceneUnitCount);
                loadingUI.SetProgress(progress, $"正在预加载场景单位 ({i + 1}/{sceneUnitCount})...");
                await ResHelper.Prepare(Database.Instance.GetIndex<UnitData>(wave.UnitId));
            }
            catch (Exception e)
            {
                if (e is NullReferenceException)
                    toRemove.Add(wave);
                TipManager.Instance.ShowTip("波次信息加载失败：" + wave.UnitId);
                Debug.LogError(e);
            }
        }

        loadingUI.SetProgress(0.98f, "正在进入战斗...");
        Pause = false;
        var battleUI = UIManager.Instance.ChangeView<BattleUI.UI_Battle>(BattleUI.UI_Battle.URL);
        battleUI.SetBattle(Battle);
        loadingUI.SetProgress(1f, "战斗加载完成");
        ExcuteTime = 0;
        await battleTcs.Task;
        Debug.Log("ExitBattleScene");
        //Battle = null;
        if (string.IsNullOrEmpty(sceneName))
        {
            await SceneManager.UnloadSceneAsync("MapBuilder", UnloadSceneOptions.UnloadAllEmbeddedSceneObjects);
        }
        else
        {
            await SceneManager.UnloadSceneAsync(sceneName, UnloadSceneOptions.UnloadAllEmbeddedSceneObjects);
        }
        EffectManager.Instance.ReturnAll();
        BulletManager.Instance.ReturnAll();
        AudioManager.Instance.PlayBackgroundAudio("main");
        //var path = Battle.Map.FindPath(Battle.Map.Grids[1, 3], Battle.Map.Grids[8, 1]);
        //foreach (var grid in path)
        //{
        //    Debug.Log(grid.X + "," + grid.Y);
        //}
    }
    public void FinishBattle()
    {
        ReSetPreviwSetting();
        battleTcs?.TrySetResult(true);

        battleExitCounter++;

        if (battleExitCounter % 3 == 0)
        {
            SpineImportHelper.Instance.UnloadAllSpineAssets();
        }

        if (battleExitCounter >= 6)
        {
            battleExitCounter = 0;
            UnifiedExpressionEngine.ClearCache();
        }

        ExtextureLoader.Instance.ClearCache();
        ResHelper.ReleasePreloadedAssets();
    }
    public void ReSetPreviwSetting()
    {
        TimeScale = 1;
        //IsPreview = false;
        RecoverPowervSpeed = 1;
        IsInfCost = false;
        IsInfHealth = false;
        IsNoCD = false;
        IsNoLimitBuild = false;
        IsInfUnitCount = false;
    }
}
