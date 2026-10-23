using UnityEngine;

/// <summary>
/// 环境参数资产：一关一份。字段全部来自官方 dump 值（ro6_b5_scene.json / AE_FlowerSea）。
/// 与 ArknEnvSetup 组件配套——组件负责把这份参数应用到目标场景。
/// </summary>
[CreateAssetMenu(fileName = "ArknEnvPreset", menuName = "Arkn/环境参数 Preset")]
public class ArknEnvPreset : ScriptableObject
{
    [Header("主光（Directional，随 prefab 子物体）")]
    public bool mainLightEnabled = true;
    [Tooltip("0=None 1=Hard 2=Soft")]
    [Range(0, 2)] public int mainLightShadows = 1;
    public Color lightColor = Color.white;
    public float lightIntensity = 2.5f;
    public Quaternion lightRotation = Quaternion.identity;

    [Header("环境光")]
    [Tooltip("SH27 反射写入 customAmbientProbe（2021.3 internal），失败自动降级 Trilight")]
    public bool useSH27 = true;
    [Tooltip("9 系数 × RGB，官方 dump 原序")]
    public float[] sh27 = new float[27];
    [Tooltip("SH 探针整体倍率：Custom 模式下 Lighting 窗口的 Ambient Intensity 无效，想调亮只能乘这里（1=官方原值）")]
    public float ambientBoost = 1f;
    public Color triSky = new Color(0.212f, 0.227f, 0.259f);
    public Color triEquator = new Color(0.114f, 0.125f, 0.133f);
    public Color triGround = new Color(0.047f, 0.043f, 0.035f);
    public float ambientIntensity = 1.4f;

    [Header("高度雾（应用到 Camera.main 上的 ArknHeightFog）")]
    public bool applyFog = true;
    public Color fogColor = new Color(0.159f, 0.152f, 0.119f);
    public float fogBaseline = 7.4f;
    public float fogTop = -6.2f;
    public float density = 0.05f;
    public float fogSpeed = 1f;

    [Header("杂项")]
    [Tooltip("关闭场景内置雾，避免与高度雾叠加")]
    public bool disableBuiltinFog = true;
}
