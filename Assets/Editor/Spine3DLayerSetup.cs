#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// 向项目添加渲染层（用于 Spine 三相机叠加渲染方案）：
///   Spine3D —— Spine 单位所在层，由 SpineOverlayCamera 渲染（叠加在最上方）；
///   Device  —— 中立单位（装置类 prefab）所在层，由 DeviceDepthCamera 写入装置深度，
///              使 Spine 与装置重合时正常深度计算。
///   FxOverlay —— 特效所在层，并入 SpineOverlayCamera 渲染，与 Spine 按深度排序，
///              避免被 Spine 完全盖住。
/// 菜单：Tools/Spine引用修复/0-创建 Spine3D/Device/FxOverlay 渲染层（只需执行一次）
/// </summary>
public static class Spine3DLayerSetup
{
    static readonly string[] LayerNames = { "Spine3D", "Device", "FxOverlay" };

    [MenuItem("Tools/Spine引用修复/0-创建 Spine3D/Device/FxOverlay 渲染层")]
    public static void CreateLayers()
    {
        var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
        if (assets == null || assets.Length == 0) { Debug.LogError("TagManager.asset 读取失败"); return; }
        var tagManager = new SerializedObject(assets[0]);
        // 注意：TagManager.asset 中用户层是 "layers" 数组的第 8~31 项（没有 userLayers 属性）
        var layers = tagManager.FindProperty("layers");
        if (layers == null || layers.arraySize < 32)
        {
            EditorUtility.DisplayDialog("渲染层", "TagManager.asset 结构异常（layers 数组缺失），请手动在 Project Settings → Tags and Layers 中添加「Spine3D」「Device」「FxOverlay」。", "OK");
            return;
        }

        var created = new System.Text.StringBuilder();
        foreach (var name in LayerNames)
        {
            bool exists = false;
            for (int i = 8; i < layers.arraySize; i++)
                if (layers.GetArrayElementAtIndex(i).stringValue == name) { exists = true; break; }
            if (exists) { created.Append($"{name}(已存在) "); continue; }

            int freeSlot = -1;
            for (int i = 8; i < layers.arraySize; i++)
            {
                if (string.IsNullOrEmpty(layers.GetArrayElementAtIndex(i).stringValue)) { freeSlot = i; break; }
            }
            if (freeSlot == -1)
            {
                EditorUtility.DisplayDialog("渲染层", "没有空闲的 User Layer 槽位，请手动在 Project Settings → Tags and Layers 中添加「Spine3D」「Device」「FxOverlay」。", "OK");
                return;
            }
            layers.GetArrayElementAtIndex(freeSlot).stringValue = name;
            created.Append($"{name}(编号 {freeSlot}) ");
        }

        tagManager.ApplyModifiedProperties();
        AssetDatabase.SaveAssets();
        Debug.Log($"[Spine3DLayerSetup] 渲染层就绪：{created}");
        EditorUtility.DisplayDialog("渲染层", $"渲染层就绪：{created}\n运行时 SpineOverlayCameraUtil 会自动使用它们。", "OK");
    }
}
#endif
