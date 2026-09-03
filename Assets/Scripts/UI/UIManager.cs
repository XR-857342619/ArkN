using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using FairyGUI;
using System.IO;
using UnityEngine.AddressableAssets;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance;

    const string PackagePath = "Assets/Bundles/UI/";
    List<string> LoadedPackages = new List<string>();

    Dictionary<string, GComponent> scenes = new Dictionary<string, GComponent>();
    GComponent nowScene;

    private void Awake()
    {
        //Debug.Log("UIManagerInit");
        Instance = this;
        DontDestroyOnLoad(gameObject);
        // 绑定全部 UI 扩展（仅注册，不在此加载包）
        LandingUI.LandingUIBinder.BindAll();
        BattleUI.BattleUIBinder.BindAll();
        MainUI.MainUIBinder.BindAll();
        DungeonUI.DungeonUIBinder.BindAll();
        MapBuilderUI.MapBuilderUIBinder.BindAll();
        DIY.DIYBinder.BindAll();

        // 启动阶段只预加载 LandingUI 包，其余 UI 包在 Init 加载流程中分帧加载，避免启动卡顿
        LoadPackge("LandingUI");
        //LoadPackge("BattleUI");
        //LoadPackge("MainUI");
        //LoadPackge("SkillIcon");
        //LoadPackge("Res");
        //LoadPackge("DungeonUI");
        //LoadPackge("MapBuilderUI");
        //LoadPackge("DIY");
        //LoadPackge("UnitFace");
        //LoadPackge("UnitPic");
    }

    private void Start()
    {

    }

    public T GetView<T>(string url) where T : GComponent
    {
        if (scenes.TryGetValue(url, out GComponent r))
        {
            return r as T;
        }
        return null;
    }

    public T ChangeView<T>(string url) where T : GComponent
    {
        var view = GetView<T>(url);
        if (view == null)
        {
            //LoadPackge(packageName);
            view = UIPackage.CreateObjectFromURL(url) as T;
            if (view == null)
            {
                Debug.LogError($"UI 创建失败：{url}，请确认对应 UI 包已加载。");
                return null;
            }
            scenes.Add((url), view);
            view.SetSize(GRoot.inst.size.x, GRoot.inst.size.y);
            view.AddRelation(GRoot.inst, RelationType.Size);
            GRoot.inst.AddChild(view);
        }
        if (nowScene != null) nowScene.visible = false;
        nowScene = view;
        view.visible = true;
        if (view is IGameUIView gameView)
        {
            gameView.Enter();
        }
        return view;
    }

    public void LoadPackge(string PackageName)
    {
        if (LoadedPackages.Contains(PackageName))
            return;
        LoadedPackages.Add(PackageName);

        byte[] bytes = null;

        // 优先通过 Addressables 加载
        try
        {
            var operation = Addressables.LoadAssetAsync<TextAsset>(PathHelper.UIPath + PackageName + "_fui");
            operation.WaitForCompletion();
            bytes = operation.Result != null ? operation.Result.bytes : null;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"Addressables 加载 UI 包失败：{PackageName}，尝试本地回退。\n{e.Message}");
        }

        //编辑器下回退到 AssetDatabase 直接读取，方便新增包未重新标记时开发调试
#if UNITY_EDITOR
        if (bytes == null)
        {
            var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<TextAsset>(PathHelper.UIPath + PackageName + "_fui.bytes");
            if (asset != null)
                bytes = asset.bytes;
        }
#endif

        if (bytes == null)
        {
            Debug.LogError($"加载 UI 包失败：{PackageName}，请确认资源已加入 Addressables 并执行 Tools→重新标记。");
            return;
        }

        UIPackage.AddPackage(bytes, PackageName, load);
    }

    object load(string name, string extension, System.Type type, out DestroyMethod destroyMethod)
    {
        destroyMethod = DestroyMethod.Unload;
        try
        {
            var op = Addressables.LoadAssetAsync<UnityEngine.Object>(PathHelper.UIPath + name);
            op.WaitForCompletion();
            return op.Result;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"Addressables 加载 UI 资源失败：{name}，尝试本地回退。\n{e.Message}");
            return null;
        }
    }

    AssetBundle getBundle(string name)
    {
        string p = Path.Combine(PathHelper.AppResPath, PathHelper.UIPath, name.ToLower());
        if (File.Exists(p))
        {
            Debug.Log(p);
            return AssetBundle.LoadFromFile(p);
        }
        else
        {
            p = Path.Combine(PathHelper.AppHotfixResPath, PathHelper.UIPath, name.ToLower());
            Debug.Log(p);
            if (File.Exists(p))
            {
                return AssetBundle.LoadFromFile(p);
            }
            else
            {
                throw new Exception("cant find" + name);
                return null;
            }
        }
    }
}