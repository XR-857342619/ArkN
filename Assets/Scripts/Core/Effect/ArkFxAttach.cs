// ============================================================
// ArkFxAttach - 特效挂载示例
// 演示两种挂法：普通节点/prefab 挂点、spine 骨骼跟随
// ============================================================
using UnityEngine;

public static class ArkFxAttach
{
    /// <summary>
    /// 把特效挂到任意 Transform（角色 prefab 的手/武器/背包挂点等）
    /// </summary>
    public static GameObject SpawnAttached(string abPath, string prefabName,
                                           Transform parent,
                                           Vector3 localOffset = default,
                                           float scale = 1f)
    {
        var fx = ArkFxLoader.SpawnEffect(abPath, prefabName, parent.position, parent.rotation);
        if (fx == null) return null;
        fx.transform.SetParent(parent, false);
        fx.transform.localPosition = localOffset;
        fx.transform.localScale = Vector3.one * scale;
        return fx;
    }

    /// <summary>
    /// spine 骨骼跟随：每帧把特效摆到指定骨骼的世界坐标。
    /// 用法：加到角色对象上，把 boneName 填成 spine 骨骼名（如 "weapon"、"hand"）。
    /// 若你用 spine-unity 的 SkeletonRenderer，也可以直接用 BoneFollower
    /// （本示例不依赖 spine 插件，通用做法）。
    /// </summary>
    public class BoneFollower : MonoBehaviour
    {
        public Transform bone;          // 目标骨骼节点（运行时从 spine 骨架取）
        public Vector3 offset;
        public float scale = 1f;

        GameObject _fx;
        string _abPath, _prefabName;

        public void SetEffect(string abPath, string prefabName)
        {
            _abPath = abPath; _prefabName = prefabName;
        }

        void LateUpdate()
        {
            if (bone == null) return;
            if (_fx == null && !string.IsNullOrEmpty(_prefabName))
            {
                _fx = ArkFxLoader.SpawnEffect(_abPath, _prefabName, bone.position, bone.rotation);
                if (_fx != null)
                {
                    _fx.transform.SetParent(bone, false);
                    _fx.transform.localPosition = offset;
                    _fx.transform.localScale = Vector3.one * scale;
                }
            }
        }
    }
}
