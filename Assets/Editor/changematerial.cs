using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

public class BatchMaterialReplacer : EditorWindow
{
    // ===== UI状态 =====
    private Material newMaterial;           // 用户拖入的目标材质
    private bool includeInactive = true;    // 是否处理未激活的子物体
    private bool replaceAllSlots = true;    // 是否替换所有材质槽，否则只替换索引0
    private int slotIndex = 0;              // 指定替换的材质槽索引
    private bool isPrefabMode = false;      // 是否处理Project中的Prefab资源

    [MenuItem("Tools/批量替换材质 (一键替换所有子物体)")]
    public static void ShowWindow() => GetWindow<BatchMaterialReplacer>("材质批量替换");

    private void OnGUI()
    {
        GUILayout.Label("将材质应用到选中物体及其所有子物体", EditorStyles.boldLabel);

        // ---- 目标材质 ----
        newMaterial = (Material)EditorGUILayout.ObjectField("目标材质", newMaterial, typeof(Material), false);

        // ---- 选项 ----
        includeInactive = EditorGUILayout.Toggle("包含未激活的子物体", includeInactive);
        replaceAllSlots = EditorGUILayout.Toggle("替换所有材质槽", replaceAllSlots);
        if (!replaceAllSlots)
            slotIndex = EditorGUILayout.IntField("指定替换的材质槽索引", slotIndex);

        isPrefabMode = EditorGUILayout.Toggle("处理Project中的Prefab资源（而非场景实例）", isPrefabMode);

        // ---- 状态提示 ----
        EditorGUILayout.Space();
        if (Selection.objects.Length == 0)
            EditorGUILayout.HelpBox("请在Hierarchy或Project中选中物体/Prefab/文件夹", MessageType.Warning);
        else if (newMaterial == null)
            EditorGUILayout.HelpBox("请拖入一个目标材质", MessageType.Warning);
        else
        {
            var selected = Selection.objects;
            var count = selected.Length;
            GUILayout.Label($"已选中 {count} 个对象，将处理其下所有渲染器");
        }

        // ---- 执行按钮 ----
        GUI.enabled = newMaterial != null && Selection.objects.Length > 0;
        if (GUILayout.Button("执行替换", GUILayout.Height(40)))
        {
            ApplyMaterialToSelection();
        }
        GUI.enabled = true;
    }

    // ========== 核心执行逻辑 ==========
    private void ApplyMaterialToSelection()
    {
        var selectedObjects = Selection.objects;

        // 区分：场景物体 vs Project中的Prefab资源
        if (isPrefabMode)
        {
            // 处理Project中的Prefab资源（直接修改资源文件）
            foreach (var obj in selectedObjects)
            {
                string assetPath = AssetDatabase.GetAssetPath(obj);
                if (string.IsNullOrEmpty(assetPath)) continue;

                // 如果是文件夹，则递归处理文件夹内的所有Prefab
                if (AssetDatabase.IsValidFolder(assetPath))
                {
                    ProcessFolder(assetPath);
                }
                else if (obj is GameObject go && PrefabUtility.GetPrefabAssetType(go) != PrefabAssetType.NotAPrefab)
                {
                    ProcessPrefabAsset(go);
                }
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
        else
        {
            // 处理场景中的GameObject（包括Prefab实例）
            List<Object> objectsToRecord = new List<Object>();
            var gameObjects = Selection.gameObjects;
            foreach (var root in gameObjects)
            {
                var renderers = root.GetComponentsInChildren<Renderer>(includeInactive);
                foreach (var r in renderers) objectsToRecord.Add(r);
            }
            Undo.RecordObjects(objectsToRecord.ToArray(), "批量替换材质");

            int total = 0;
            foreach (var root in gameObjects)
            {
                total += ApplyToRendererList(root.GetComponentsInChildren<Renderer>(includeInactive));
            }
            Debug.Log($"✅ 场景替换完成，共修改 {total} 个渲染器。");
        }
    }

    // ----- 处理单个Prefab资源（Project中的） -----
    private void ProcessPrefabAsset(GameObject prefabAsset)
    {
        // 加载Prefab内容（临时编辑模式）
        string path = AssetDatabase.GetAssetPath(prefabAsset);
        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var renderers = prefabRoot.GetComponentsInChildren<Renderer>(true);
            int count = ApplyToRendererList(renderers);
            if (count > 0)
            {
                PrefabUtility.SaveAsPrefabAsset(prefabRoot, path);
                Debug.Log($"✅ 已修改Prefab: {path}，替换了 {count} 个材质");
            }
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }

    // ----- 递归处理文件夹（查找所有Prefab） -----
    private void ProcessFolder(string folderPath)
    {
        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { folderPath });
        foreach (var guid in prefabGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab != null) ProcessPrefabAsset(prefab);
        }
        // 子文件夹递归
        foreach (string subDir in AssetDatabase.GetSubFolders(folderPath))
        {
            ProcessFolder(subDir);
        }
    }

    // ----- 实际替换材质的核心方法（处理渲染器列表） -----
    private int ApplyToRendererList(Renderer[] renderers)
    {
        int count = 0;
        foreach (var renderer in renderers)
        {
            if (renderer == null) continue;

            Material[] materials = renderer.sharedMaterials;
            if (replaceAllSlots)
            {
                // 替换所有材质槽
                for (int i = 0; i < materials.Length; i++)
                {
                    materials[i] = newMaterial;
                }
            }
            else
            {
                // 只替换指定索引（如果索引有效）
                if (slotIndex >= 0 && slotIndex < materials.Length)
                {
                    materials[slotIndex] = newMaterial;
                }
                else
                {
                    Debug.LogWarning($"跳过 {renderer.name}，索引 {slotIndex} 超出范围");
                    continue;
                }
            }
            renderer.sharedMaterials = materials;
            count++;
        }
        return count;
    }
}