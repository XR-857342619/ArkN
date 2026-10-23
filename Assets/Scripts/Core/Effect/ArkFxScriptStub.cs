// ============================================================
// ArkFxScriptStub - 原版缺脚本组件的"存档占位"
// 把 fxparams.json 里读到的类名和原始字段 JSON 挂在节点上，
// 供后续逐类做替身逻辑（ProjectileEffect/PauseEffectIf 等）时取用，
// 也方便排查"这个节点原本挂了什么"。
// ============================================================
using UnityEngine;

public class ArkFxScriptStub : MonoBehaviour
{
    public string className;
    [TextArea(3, 20)]
    public string rawJson;
}
