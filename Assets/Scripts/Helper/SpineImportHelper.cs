using UnityEngine;
using Spine;
using Spine.Unity;
using System;
using System.IO;
using System.Collections.Generic;

public class SpineImportHelper : MonoBehaviour
{
    // 存储已加载的骨骼数据资源
    public Dictionary<string, SkeletonDataAsset> loadedSkeletons = new Dictionary<string, SkeletonDataAsset>();
    private Material _baseMaterial;

    // 单例实例
    private static SpineImportHelper _instance;
    public static SpineImportHelper Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindObjectOfType<SpineImportHelper>();
                if (_instance == null)
                {
                    GameObject obj = new GameObject("SpineImportHelper");
                    _instance = obj.AddComponent<SpineImportHelper>();
                    DontDestroyOnLoad(obj);
                }
            }
            return _instance;
        }
    }

    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
            DontDestroyOnLoad(gameObject);
            Init();
        }
        else if (_instance != this)
        {
            Destroy(gameObject);
        }
    }

    public void Init()
    {
        if (_baseMaterial == null)
        {
            Shader spineShader = Shader.Find("Spine/Skeleton Tint");
            if (spineShader != null)
            {
                _baseMaterial = new Material(spineShader);
                _baseMaterial.renderQueue = 3000;
            }
            else
            {
                Debug.LogError("找不到Spine Shader: Spine/Skeleton Tint，请确保Spine资源导入正确");
            }
        }
    }

    /// <summary>
    /// 加载Spine资源（适配Unity 2021+的二进制加载方式）
    /// </summary>
    public SkeletonDataAsset LoadSpineAssets(string key, string texturePath, string atlasTextPath, string skeletonBytePath)
    {
        if (loadedSkeletons.TryGetValue(key, out SkeletonDataAsset existingAsset))
        {
            return existingAsset;
        }

        if (_baseMaterial == null)
        {
            Init();
            if (_baseMaterial == null)
            {
                Debug.LogError("基础材质初始化失败，无法加载Spine资源");
                return null;
            }
        }

        try
        {
            // 加载纹理
            Texture2D texture = LoadTexture(texturePath);
            if (texture == null) return null;

            // 加载图集文本
            TextAsset atlasText = LoadTextAsset(atlasTextPath);
            if (atlasText == null)
            {
                Destroy(texture);
                return null;
            }

            // 加载骨骼二进制数据（Unity 2021+兼容方式）
            byte[] skeletonBytes = LoadSkeletonBytes(skeletonBytePath);
            if (skeletonBytes == null)
            {
                Destroy(texture);
                Destroy(atlasText);
                return null;
            }

            // 创建图集资产
            SpineAtlasAsset atlasAsset = SpineAtlasAsset.CreateRuntimeInstance(
                atlasText, new[] { texture }, _baseMaterial, true);

            // 关键修复：直接使用字节数组创建SkeletonData，避免使用TextAsset
            SkeletonData skeletonData = LoadSkeletonDataFromBytes(skeletonBytes, atlasAsset);
            if (skeletonData == null)
            {
                Destroy(atlasAsset);
                Destroy(texture);
                Destroy(atlasText);
                return null;
            }

            // 创建并配置SkeletonDataAsset
            SkeletonDataAsset skeletonAsset = ScriptableObject.CreateInstance<SkeletonDataAsset>();
            skeletonAsset.skeletonJSON = null; // 确保不使用JSON
            skeletonAsset.atlasAssets = new[] { atlasAsset };
            skeletonAsset.fromAnimation = new string[0];
            skeletonAsset.toAnimation = new string[0];
            skeletonAsset.duration = new float[0];

            // 使用反射设置内部的skeletonData（Spine 3.8兼容）
            var dataField = typeof(SkeletonDataAsset).GetField("skeletonData",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (dataField != null)
            {
                dataField.SetValue(skeletonAsset, skeletonData);
                loadedSkeletons[key] = skeletonAsset;
                Debug.Log("设置SkeletonDataAsset的骨骼数据" + skeletonData.Name);
                return skeletonAsset;
            }
            else
            {
                Debug.LogError("无法设置SkeletonDataAsset的骨骼数据，可能是Spine版本不兼容");
                Destroy(skeletonAsset);
                Destroy(atlasAsset);
                return null;
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"加载Spine资源时发生错误: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// 直接加载骨骼二进制字节，不通过TextAsset
    /// </summary>
    private byte[] LoadSkeletonBytes(string path)
    {
        if (!File.Exists(path))
        {
            Debug.LogError($"骨骼二进制文件不存在: {path}");
            return null;
        }

        try
        {
            return File.ReadAllBytes(path);
        }
        catch (Exception ex)
        {
            Debug.LogError($"加载骨骼二进制文件错误 {path}: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// 从字节数组加载骨骼数据
    /// </summary>
    private SkeletonData LoadSkeletonDataFromBytes(byte[] data, SpineAtlasAsset atlasAsset)
    {
        try
        {
            // 获取图集数据
            Atlas atlas = atlasAsset.GetAtlas();
            if (atlas == null)
            {
                Debug.LogError("图集数据获取失败");
                return null;
            }

            // 使用Spine的BinarySkeletonLoader直接加载字节数据
            SkeletonBinary binaryLoader = new SkeletonBinary(atlas);
            binaryLoader.Scale = 1f; // 根据需要调整缩放
            return binaryLoader.ReadSkeletonData(new MemoryStream(data));
        }
        catch (Exception ex)
        {
            Debug.LogError($"解析骨骼二进制数据失败: {ex.Message}");
            return null;
        }
    }

    // 其他辅助方法保持不变...
    private Texture2D LoadTexture(string path)
    {
        if (!File.Exists(path))
        {
            Debug.LogError($"纹理文件不存在: {path}");
            return null;
        }

        try
        {
            byte[] data = File.ReadAllBytes(path);
            Texture2D texture = new Texture2D(2, 2, TextureFormat.ARGB32, false);
            if (texture.LoadImage(data))
            {
                texture.name = Path.GetFileNameWithoutExtension(path);
                texture.wrapMode = TextureWrapMode.Clamp;
                texture.filterMode = FilterMode.Bilinear;
                return texture;
            }

            Debug.LogError($"无法加载纹理: {path}");
            Destroy(texture);
            return null;
        }
        catch (Exception ex)
        {
            Debug.LogError($"加载纹理错误 {path}: {ex.Message}");
            return null;
        }
    }

    private TextAsset LoadTextAsset(string path)
    {
        if (!File.Exists(path))
        {
            Debug.LogError($"文本文件不存在: {path}");
            return null;
        }

        try
        {
            byte[] data = File.ReadAllBytes(path);
            string content = System.Text.Encoding.UTF8.GetString(data);
            TextAsset textAsset = new TextAsset(content);
            textAsset.name = Path.GetFileNameWithoutExtension(path);
            return textAsset;
        }
        catch (Exception ex)
        {
            Debug.LogError($"加载文本错误 {path}: {ex.Message}");
            return null;
        }
    }

    public void UnloadSpineAssets(string key)
    {
        if (loadedSkeletons.TryGetValue(key, out SkeletonDataAsset asset))
        {
            if (asset.atlasAssets != null)
            {
                foreach (var atlas in asset.atlasAssets)
                {
                    Destroy(atlas);
                }
            }
            Destroy(asset);
            loadedSkeletons.Remove(key);
        }
    }

    public void UnloadAllSpineAssets()
    {
        foreach (var asset in loadedSkeletons.Values)
        {
            if (asset.atlasAssets != null)
            {
                foreach (var atlas in asset.atlasAssets)
                {
                    Destroy(atlas);
                }
            }
            Destroy(asset);
        }
        loadedSkeletons.Clear();
    }

    private void OnDestroy()
    {
        UnloadAllSpineAssets();
        if (_baseMaterial != null)
        {
            Destroy(_baseMaterial);
        }
    }
}
