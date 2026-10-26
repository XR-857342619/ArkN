// ============================================================
// SpineBoneDump - 打印 Spine 骨架的「骨骼索引 -> 骨骼名」对照表
//
// 用途：验证方舟原版 FollowMountPoint._mountPointType 的 5~29
//       是否就是 skeleton.bones 的数组下标。
//
// 用法：
//   1. Project 窗口选中一个 *_SkeletonData.asset（或任意引用它的 prefab）
//   2. 菜单 Tools/ArkFx/7. 打印选中 Spine 骨架的骨骼索引表
//   3. 或直接点 Tools/ArkFx/7b. 批量打印前 N 个单位的骨骼表
// ============================================================
#if UNITY_EDITOR
using System.Text;
using Spine;
using Spine.Unity;
using UnityEditor;
using UnityEngine;

public static class SpineBoneDump
{
    const int ShowCount = 32;

    [MenuItem("Tools/ArkFx/7. 打印选中 Spine 骨架的骨骼索引表", false, 7)]
    static void DumpSelected()
    {
        var obj = Selection.activeObject;
        if (obj == null) { Debug.LogWarning("[SpineBoneDump] 先选中一个 SkeletonDataAsset"); return; }

        var sda = obj as SkeletonDataAsset;
        if (sda == null)
        {
            string path = AssetDatabase.GetAssetPath(obj);
            sda = AssetDatabase.LoadAssetAtPath<SkeletonDataAsset>(path);
        }
        if (sda == null) { Debug.LogWarning("[SpineBoneDump] 选中的不是 SkeletonDataAsset: " + obj.name); return; }

        var sd = sda.GetSkeletonData(false);
        if (sd == null) { Debug.LogWarning("[SpineBoneDump] 无法读取 SkeletonData"); return; }

        var sb = new StringBuilder();
        sb.AppendLine($"[SpineBoneDump] {sda.name}  共 {sd.Bones.Count} 根骨骼");
        int n = Mathf.Min(ShowCount, sd.Bones.Count);
        for (int i = 0; i < n; i++)
            sb.AppendLine($"    [{i,2}] {sd.Bones.Items[i].Name}");
        Debug.Log(sb.ToString());
    }

    [MenuItem("Tools/ArkFx/7. 打印选中 Spine 骨架的骨骼索引表", true)]
    static bool DumpSelectedValidate() { return Selection.activeObject != null; }

    [MenuItem("Tools/ArkFx/7b. 批量打印单位骨骼表（前 8 个）", false, 8)]
    static void DumpBatch()
    {
        var guids = AssetDatabase.FindAssets("t:SkeletonDataAsset", new[] { "Assets/Res/Spine" });
        int done = 0;
        var sb = new StringBuilder();
        foreach (var g in guids)
        {
            string p = AssetDatabase.GUIDToAssetPath(g);
            var sda = AssetDatabase.LoadAssetAtPath<SkeletonDataAsset>(p);
            if (sda == null) continue;
            var sd = sda.GetSkeletonData(false);
            if (sd == null) continue;
            sb.AppendLine($"[{sda.name}] 骨骼数={sd.Bones.Count}");
            int n = Mathf.Min(12, sd.Bones.Count);
            for (int i = 0; i < n; i++)
                sb.Append($"  {i}:{sd.Bones.Items[i].Name}");
            sb.AppendLine();
            if (++done >= 8) break;
        }
        Debug.Log("[SpineBoneDump] 批量结果：\n" + sb);
    }
}
#endif
