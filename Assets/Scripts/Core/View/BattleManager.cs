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
        // R7：地图解析失败时 GetMap 返回 null（不抛异常），这里必须兜住，否则下面 mapInfo.Scene 直接 NRE
        if (mapInfo == null)
        {
            Debug.LogError($"[BattleManager] 进入战斗失败：地图 {battleConfig.MapPackage}/{battleConfig.MapName} 读取或解析失败");
            TipManager.Instance.ShowTip("地图数据读取失败，请检查该地图存档");
            Pause = false;
            UIManager.Instance.ChangeView<MainUI.UI_Main>(MainUI.UI_Main.URL);
            return;
        }
        loadingUI.SetProgress(0.05f, "正在读取地图数据...");

        // === R5：进图前把本图用到的皮肤 prefab 先异步预载完 ===
        // 之后 MapManager.Build / ApplySkins 里逐格 ResHelper.Instantiate 全部命中缓存，
        // 不再在主线程上连续阻塞加载（单图最多一百多格皮肤，以前进战斗会明显卡一下）。
        if (FeatureFlags.SkinBrush)
        {
            int skinCount = SkinResourcePreloader.Count(mapInfo);
            if (skinCount > 0)
                loadingUI.SetProgress(0.06f, $"正在预载地块皮肤 (0/{skinCount})...");
            await SkinResourcePreloader.PrepareAsync(mapInfo, (done, total, skinName) =>
            {
                if (total <= 0) return;
                float p = Mathf.Lerp(0.06f, 0.20f, (float)done / total);
                loadingUI.SetProgress(p, done < total
                    ? $"正在预载地块皮肤 ({done}/{total})：{skinName}"
                    : $"地块皮肤预载完成 ({total}/{total})");
            });
        }

        var sceneName = mapInfo.Scene;
        loadingUI.SetProgress(0.2f, "正在加载场景...");
        if (string.IsNullOrEmpty(sceneName))
        {
            // === 防重复叠加：从造图界面直接进战斗时 MapBuilder 场景已在内存里，
            // 直接 Additive 再加载一份 = 同名场景 ×2 = 方向光翻倍（进战斗立刻过曝的根因）。先卸旧的再加载 ===
            await UnloadAllSceneCopiesAsync("MapBuilder");
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
            // 同上：目标地图场景若已在内存（造图会话遗留），先卸再加载，防止叠加
            await UnloadAllSceneCopiesAsync(sceneName);
            await SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
            // === 皮肤笔刷补丁：场景地图不走 Build，需按存档数据给场景格子还原皮肤 ===
            MapManager.Instance.ApplySkins(mapInfo.GridInfos);
        }
        SceneManager.SetActiveScene(SceneManager.GetSceneByName(sceneName));
        // 皮肤已全部实例化完毕：释放预载持有的**资产**引用（实例各自持引用，不会消失）
        SkinResourcePreloader.ReleaseAll();
        loadingUI.SetProgress(0.25f, "场景加载完成");
        // 按地图配置播放 BGM；旧地图没有 BGM 字段时回退到默认曲目
        AudioManager.Instance.PlayBackgroundAudio(
            string.IsNullOrEmpty(mapInfo.BGM) ? AudioManager.DefaultBgmAddress : mapInfo.BGM);
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

        loadingUI.SetProgress(0.96f, "正在加载音效...");
        await AudioManager.Instance.PreloadSoundEffects("落地", "死亡", "开大");

        loadingUI.SetProgress(0.98f, "正在进入战斗...");
        Pause = false;
        var battleUI = UIManager.Instance.ChangeView<BattleUI.UI_Battle>(BattleUI.UI_Battle.URL);
        battleUI.SetBattle(Battle);
        loadingUI.SetProgress(1f, "战斗加载完成");
        ExcuteTime = 0;
        await battleTcs.Task;
        Debug.Log("ExitBattleScene");
        //Battle = null;
        // === 修复场景卸载泄漏（v2）：同名场景可能有多份（如造图会话 + 战斗各加载一份 MapBuilder），
        // GetSceneByName/UnloadSceneAsync(名字) 只卸第一个匹配——漏掉的继续叠光。改成按句柄循环卸载所有同名场景 ===
        if (!string.IsNullOrEmpty(sceneName))
        {
            await UnloadAllSceneCopiesAsync(sceneName);
        }
        await UnloadAllSceneCopiesAsync("MapBuilder");
        EffectManager.Instance.ReturnAll();
        BulletManager.Instance.ReturnAll();
        AudioManager.Instance.PlayBackgroundAudio("生命流");
        //var path = Battle.Map.FindPath(Battle.Map.Grids[1, 3], Battle.Map.Grids[8, 1]);
        //foreach (var grid in path)
        //{
        //    Debug.Log(grid.X + "," + grid.Y);
        //}
    }
    /// <summary>把内存里所有同名已加载场景全部卸载（同名场景可能有多份，按名字卸只会卸一份）。</summary>
    static async Task UnloadAllSceneCopiesAsync(string name)
    {
        int guard = 0;
        while (guard++ < 8)
        {
            Scene hit = default;
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var sc = SceneManager.GetSceneAt(i);
                if (sc.isLoaded && sc.name == name) { hit = sc; break; }
            }
            if (!hit.IsValid()) break;
            await SceneManager.UnloadSceneAsync(hit, UnloadSceneOptions.UnloadAllEmbeddedSceneObjects);
        }
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
        AudioManager.Instance.ReleasePreloadedSoundEffects();
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
