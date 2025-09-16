using UnityEngine;
using Spine;
using Spine.Unity;
using System;
using System.IO;
using System.Text;
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
                // 查找场景中是否已有实例
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
        // 确保单例唯一性
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

    /// <summary>
    /// 初始化基础材质
    /// </summary>
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
    /// 加载Spine资源并创建运行时资产
    /// </summary>
    /// <param name="key">用于缓存的唯一键</param>
    /// <param name="texturePath">纹理路径</param>
    /// <param name="atlasTextPath">图集文本路径</param>
    /// <param name="skeletonBytePath">骨骼二进制文件路径</param>
    /// <returns>加载成功的骨骼数据资源，失败则返回null</returns>
    public SkeletonDataAsset LoadSpineAssets(string key, string texturePath, string atlasTextPath, string skeletonBytePath)
    {
        // 检查是否已加载
        if (loadedSkeletons.TryGetValue(key, out SkeletonDataAsset existingAsset))
        {
            return existingAsset;
        }

        // 检查基础材质是否初始化
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

            // 加载骨骼数据
            TextAsset skeletonData = LoadBinaryAsset(skeletonBytePath);
            if (skeletonData == null)
            {
                Destroy(texture);
                Destroy(atlasText);
                return null;
            }

            // 创建Spine运行时资源
            SpineAtlasAsset atlasAsset = SpineAtlasAsset.CreateRuntimeInstance(
                atlasText, new[] { texture }, _baseMaterial, true);

            SkeletonDataAsset skeletonAsset = SkeletonDataAsset.CreateRuntimeInstance(
                skeletonData, atlasAsset, true);

            // 预加载骨骼数据
            try
            {
                skeletonAsset.GetSkeletonData(false);
                loadedSkeletons[key] = skeletonAsset;
                return skeletonAsset;
            }
            catch (Exception ex)
            {
                Debug.LogError($"骨骼数据加载失败: {ex.Message}");
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
    /// 释放指定键的Spine资源
    /// </summary>
    public void UnloadSpineAssets(string key)
    {
        if (loadedSkeletons.TryGetValue(key, out SkeletonDataAsset asset))
        {
            // 释放相关资源
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

    /// <summary>
    /// 释放所有加载的Spine资源
    /// </summary>
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

    /// <summary>
    /// 加载纹理
    /// </summary>
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

    /// <summary>
    /// 加载文本资源
    /// </summary>
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
            string content = Encoding.UTF8.GetString(data);
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

    /// <summary>
    /// 加载二进制资源
    /// </summary>
    private TextAsset LoadBinaryAsset(string path)
    {
        if (!File.Exists(path))
        {
            Debug.LogError($"二进制文件不存在: {path}");
            return null;
        }

        try
        {
            byte[] data = File.ReadAllBytes(path);
            // 使用反射设置二进制数据（Spine二进制文件需要这样处理）
            TextAsset textAsset = new TextAsset();
            var field = typeof(TextAsset).GetField("m_Data",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field?.SetValue(textAsset, data);
            textAsset.name = Path.GetFileNameWithoutExtension(path).Replace("skel","json");
            Log.Debug("name: " + textAsset.name);
            return textAsset;
        }
        catch (Exception ex)
        {
            Debug.LogError($"加载二进制错误 {path}: {ex.Message}");
            return null;
        }
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
