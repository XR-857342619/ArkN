#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// 打包前守卫：凡是运行时靠 Shader.Find(名字) 取的 shader，都必须出现在
/// Project Settings → Graphics → Always Included Shaders 里，否则会被打包剥离，
/// 玩家包里 Shader.Find 返回 null。
///
/// 后果不是"没效果"而往往是"变成另一种错误画面"：
///   - Custom/DeviceDepthOnly 缺失 → DeviceDepthCamera 失去替换着色器，
///     会用彩色材质把高台/装置再画一遍，而它的深度缓冲已被清空（没有地面）
///     → 高台埋在地下的部分也露出来（安卓打包后出现过的地形重影 bug）。
///   - Arkn/HeightFog 缺失 → 高度雾静默失效。
///
/// 本脚本在每次打包前自动把下表里的 shader 补进 Always Included Shaders。
/// 以后新增"只在运行时 Shader.Find"的 shader，把名字加进 RequiredShaderNames 即可。
/// </summary>
public class BuildShaderGuard : IPreprocessBuildWithReport
{
    public int callbackOrder => 0;

    static readonly string[] RequiredShaderNames =
    {
        "Custom/DeviceDepthOnly", // SpineOverlayCameraUtil / SpineDepthOccluderUtil
        "Arkn/HeightFog",         // ArknEnvSetup/ArknHeightFog.cs
    };

    public void OnPreprocessBuild(BuildReport report)
    {
        var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset");
        if (assets == null || assets.Length == 0)
        {
            Debug.LogError("[BuildShaderGuard] 读不到 ProjectSettings/GraphicsSettings.asset，跳过检查");
            return;
        }

        var so = new SerializedObject(assets[0]);
        var list = so.FindProperty("m_AlwaysIncludedShaders");
        if (list == null)
        {
            Debug.LogError("[BuildShaderGuard] GraphicsSettings 里找不到 m_AlwaysIncludedShaders");
            return;
        }

        var added = new List<string>();
        foreach (var name in RequiredShaderNames)
        {
            var shader = Shader.Find(name);
            if (shader == null)
            {
                Debug.LogError($"[BuildShaderGuard] 工程里找不到 shader「{name}」，请确认文件名与 Shader \"...\" 声明一致");
                continue;
            }

            bool exists = false;
            for (int i = 0; i < list.arraySize; i++)
            {
                if (list.GetArrayElementAtIndex(i).objectReferenceValue == shader) { exists = true; break; }
            }
            if (exists) continue;

            list.InsertArrayElementAtIndex(list.arraySize);
            list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = shader;
            added.Add(name);
        }

        if (added.Count > 0)
        {
            so.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();
            Debug.LogWarning("[BuildShaderGuard] 已自动补进 Always Included Shaders：" + string.Join("、", added) +
                             "（请把 ProjectSettings/GraphicsSettings.asset 一并提交）");
        }
        else
        {
            Debug.Log("[BuildShaderGuard] 运行时 Shader.Find 依赖的 shader 均已包含，OK");
        }
    }
}
#endif
