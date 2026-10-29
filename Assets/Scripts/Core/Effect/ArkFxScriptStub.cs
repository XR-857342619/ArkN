// ============================================================
// ArkFxScriptStub - 原版缺脚本组件的"存档占位"
// 把 fxparams.json 里读到的类名和原始字段 JSON 挂在节点上，
// 供后续逐类做替身逻辑（ProjectileEffect/PauseEffectIf 等）时取用，
// 也方便排查"这个节点原本挂了什么"。
//
// P1：参数管线扩展后，同一个节点可能同时有多个未实现类；
// 因此新增 entries 列表，保留 className/rawJson 作为单条兼容字段。
// ============================================================
using System.Collections.Generic;
using UnityEngine;

public class ArkFxScriptStub : MonoBehaviour
{
    [System.Serializable]
    public class Entry
    {
        public string className;
        [TextArea(2, 12)]
        public string rawJson;
    }

    [Tooltip("该节点上收集到的原版脚本参数（可多条）")]
    public List<Entry> entries = new List<Entry>();

    // 兼容旧代码/旧 prefab：保存第一条记录的类名与原始 JSON
    public string className;
    [TextArea(3, 20)]
    public string rawJson;

    [Tooltip("是否已经由 BakePrefab 烘焙过（用于避免重复压亮度）")]
    public bool baked;

    /// <summary>追加一条原版脚本参数；同名的同节点重复调用会被跳过。</summary>
    public void Add(string cls, string raw)
    {
        if (string.IsNullOrEmpty(cls)) return;

        for (int i = 0; i < entries.Count; i++)
        {
            var existing = entries[i];
            if (existing != null && existing.className == cls && existing.rawJson == raw)
                return;
        }

        entries.Add(new Entry { className = cls, rawJson = raw });
        if (string.IsNullOrEmpty(className))
        {
            className = cls;
            rawJson = raw;
        }
    }
}
