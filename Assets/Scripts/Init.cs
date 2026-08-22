using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

public class Init : MonoBehaviour
{
    public static Init Instance;
    private void Awake()
    {
        Instance = this;
        DontDestroyOnLoad(gameObject);
        TaskScheduler.UnobservedTaskException += (sender, args) =>
        {
            if (args.Exception.InnerException != null)
            {
                Debug.LogError(args.Exception.InnerException);
            }
            else
                Debug.LogError(args.Exception);
        };
    }

    private async void Start()
    {
        // 1. 先显示 LandingPage，后续加载过程可视化分帧执行
        var landing = UIManager.Instance.ChangeView<LandingUI.UI_LandingPage>(LandingUI.UI_LandingPage.URL);
        landing?.SetProgress(0f, "正在初始化...");

        // 2. 初始化 Addressables
        landing?.SetProgress(0.03f, "正在初始化资源系统...");
        await UnityEngine.AddressableAssets.Addressables.InitializeAsync().Task;
        await Task.Yield();

        // 2.5. 首次启动时复制 StreamingAssets 到持久化路径（Android 必须）
        landing?.SetProgress(0.05f, "正在复制初始资源...");
        await RunCopyOnFirstLaunch();
        await Task.Yield();

        // 3. 分帧加载其余 UI 包
        await LoadUiPackages(landing);

        // 4. 加载配置数据
        landing?.SetProgress(0.35f, "正在加载配置数据...");
        UnifiedExpressionEngine.ClearCache();
        await Database.Instance.Init();
        await Task.Yield();

        // 5. 初始化玩家存档/编队数据
        landing?.SetProgress(0.7f, "正在初始化玩家数据...");
        GameData.Instance.Init();
        await Task.Yield();

        // 6. 加载/播放背景音乐
        landing?.SetProgress(0.85f, "正在加载音频...");
        AudioManager.Instance.PlayBackgroundAudio("main");
        await Task.Yield();

        // 7. 完成，淡出加载页并进入主界面
        landing?.SetProgress(1f, "准备进入主界面...");
        landing?.Complete();
        await Task.Yield();

        var battleUI = UIManager.Instance.ChangeView<MainUI.UI_Main>(MainUI.UI_Main.URL);
    }

    /// <summary>
    /// 将 StreamingAssetsCopyUtility.CopyOnFirstLaunch 协程包装为 Task，供 async 流程等待。
    /// </summary>
    private static Task RunCopyOnFirstLaunch()
    {
        var tcs = new TaskCompletionSource<bool>();
        Instance.StartCoroutine(CopyWrapper(tcs));
        return tcs.Task;
    }

    private static IEnumerator CopyWrapper(TaskCompletionSource<bool> tcs)
    {
        yield return StreamingAssetsCopyUtility.CopyOnFirstLaunch();
        tcs.SetResult(true);
    }

    /// <summary>
    /// 分帧加载 LandingPage 之外的 UI 包，并同步更新进度条与描述。
    /// </summary>
    private async Task LoadUiPackages(LandingUI.UI_LandingPage landing)
    {
        string[] packages =
        {
            "BattleUI",
            "MainUI",
            "SkillIcon",
            "Res",
            "DungeonUI",
            "MapBuilderUI",
            "DIY"
        };

        const float startProgress = 0.05f;
        const float endProgress = 0.35f;

        for (int i = 0; i < packages.Length; i++)
        {
            float progress = Mathf.Lerp(startProgress, endProgress, packages.Length == 1 ? 1f : (float)i / (packages.Length - 1));
            landing?.SetProgress(progress, $"正在加载界面: {packages[i]}...");

            UIManager.Instance.LoadPackge(packages[i]);

            // 让出一帧，使 UI 有机会刷新
            await Task.Yield();
        }
    }
}