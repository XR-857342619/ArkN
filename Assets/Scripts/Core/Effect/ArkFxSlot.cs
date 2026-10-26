// ============================================================
// ArkFxSlot - 特效槽组件
// 挂到你的角色/武器 prefab 上，Inspector 里配置即可，
// 运行时自动从 AB 实例化特效并挂到指定挂点。
//
// 字段说明：
//   ab File        主包文件名，如 firstar_lz4.ab（在 ab/ 目录里找）
//   prefab Name    特效名，从 prefab清单.md 里抄，如 firstar_skill_01_start
//   socket Path    挂点（本对象下的子节点路径），留空 = 挂在自身
//   local Offset   挂点局部偏移（特效原点一般在角色脚底，挂手部时要调）
//   local Euler    局部旋转（度）
//   scale          整体缩放
//   play On Enable 对象激活时自动播放；关闭后可手动调 Play()
// ============================================================
using System.IO;
using UnityEngine;

public class ArkFxSlot : MonoBehaviour
{
    [Header("特效来源（ab/ 目录下）")]
    public string abFile = "firstar_lz4.ab";
    public string prefabName = "";

    [Header("挂点与变换")]
    public string socketPath = "";
    public Vector3 localOffset;
    public Vector3 localEuler;
    public float scale = 1f;

    [Header("播放")]
    public bool playOnEnable = true;
    [Tooltip("运行时按此键重播特效（None = 关闭）。Play 模式下生效。")]
    public KeyCode replayKey = KeyCode.R;
    [Tooltip("重播时随机一点偏移，方便观察粒子形态")]
    public bool replayJitter = false;

    GameObject _fx;
    Transform _socket;

    static string AbDir => Path.Combine(PathHelper.AppHotfixResPath, "arkfx");

    void Awake() => ResolveSocket();

    void ResolveSocket()
    {
        _socket = string.IsNullOrEmpty(socketPath)
            ? transform
            : transform.Find(socketPath);
        if (_socket == null)
        {
            Debug.LogWarning($"[ArkFxSlot] {name}: 找不到挂点 {socketPath}，改挂自身");
            _socket = transform;
        }
    }

    void OnEnable()
    {
        if (playOnEnable) Play();
    }

    void OnDisable()
    {
        if (_fx != null) { Destroy(_fx); _fx = null; }
    }

    void Update()
    {
        if (replayKey != KeyCode.None && Input.GetKeyDown(replayKey))
        {
            Debug.Log($"[ArkFxSlot] {name}: 检测到 {replayKey} 键，重播 {prefabName}");
            Play();
        }
    }

    /// <summary>手动触发播放（供技能逻辑调用）</summary>
    public void Play()
    {
        if (string.IsNullOrEmpty(prefabName) || !Directory.Exists(AbDir)) return;

        // 重新生成一份新实例（重复 Play = 重播一次特效）
        if (_fx != null) Destroy(_fx);

        var abPath = Path.Combine(AbDir, Path.GetFileName(abFile)); // 允许误填目录，只取文件名
        _fx = ArkFxLoader.SpawnEffect(abPath, prefabName, _socket.position, _socket.rotation);
        if (_fx == null) { Debug.LogWarning($"[ArkFxSlot] {name}: 实例化失败"); return; }
        Debug.Log($"[ArkFxSlot] {name}: 已实例化 {_fx.name}");

        _fx.transform.SetParent(_socket, false);
        _fx.transform.localPosition = replayJitter
            ? localOffset + new Vector3(Random.Range(-0.3f, 0.3f), 0, Random.Range(-0.3f, 0.3f))
            : localOffset;
        _fx.transform.localEulerAngles = localEuler;
        _fx.transform.localScale = Vector3.one * scale;
    }
}
