using UnityEngine;
using UnityEditor;
using Spine;
using Spine.Unity; // 确保包含Spine命名空间
using System.Collections;
using System.Collections.Generic;
using System.IO;
using FairyGUI;
using System;
using System.Text;

public class SpineImportHelper : MonoBehaviour
{
    public Dictionary<string, SkeletonDataAsset> animations = new Dictionary<string, SkeletonDataAsset>();
    public Material _baseMaterial;
    //public TextAsset skeletonByte;
    //public TextAsset atlasText;
    //public Texture2D[] textures;
    public string texturePath;
    //public Material materialPropertySource;

    private static SpineImportHelper _instance;
    public static SpineImportHelper Instance
    {
        get
        {
            if (_instance == null)
            {
                GameObject obj = new GameObject("SpineLoader");
                _instance = obj.AddComponent<SpineImportHelper>();
                DontDestroyOnLoad(obj);
            }
            return _instance;
        }
    }

    public void Init()
    {
        //material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Res/Spine/Unit/char_002_amiya/front/char_002_amiya_Materail.mat");
        // 初始化基础材质，使用Spine的默认Shader
        _baseMaterial = new Material(Shader.Find("Spine/Skeleton Tint"));
        //material.SetFloat("_angle", 60);
        //_baseMaterial.renderQueue = 3000; // 确保渲染顺序
        //Debug.Log(_baseMaterial.shader.name);
    }


    SpineAtlasAsset runtimeAtlasAsset;
    public SkeletonDataAsset runtimeSkeletonDataAsset;
    SkeletonAnimation runtimeSkeletonAnimation;

    public void CreateRuntimeAssetsAndGameObject(string texturePath, string atlasTextPath, string skeletonBytePath)
    {
        // 1. Create the AtlasAsset (needs atlas text asset and textures, and materials/shader);
        // 2. Create SkeletonDataAsset (needs json or binary asset file, and an AtlasAsset)
        // 3. Create SkeletonAnimation (needs a valid SkeletonDataAsset)
        byte[] data;
        TextAsset atlasText = null;
        TextAsset skeletonByte = null;
        Texture2D texture = null;
        if (File.Exists(texturePath))
        {
            data = File.ReadAllBytes(texturePath);
            texture = new Texture2D(2, 2);
            texture.LoadImage(data);
            texture.name = Path.GetFileNameWithoutExtension(texturePath);
            data = null;
        }
        else
        {
            Debug.LogError("Texture file not found: " + texturePath);
            return;
        }
        if (File.Exists(atlasTextPath))
        {
            data = File.ReadAllBytes(atlasTextPath);
            //string atlasTextStr = Encoding.UTF8.GetString(data);
            atlasText = new TextAsset(Encoding.UTF8.GetString(data));
            data = null;
        }
        else
        {
            Debug.LogError("Atlas text file not found: " + atlasTextPath);
            return;
        }
        if (File.Exists(skeletonBytePath))
        {
            data = File.ReadAllBytes(skeletonBytePath);
            //System.Reflection.FieldInfo field = typeof(TextAsset).GetField(
            //    "m_Data",
            //    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance
            //);
            //field.SetValue(skeletonByte, data);
            ////skeletonByte = new TextAsset.CreateInstance(data);
            skeletonByte = new TextAsset(Encoding.UTF8.GetString(data));
            skeletonByte.name = Path.GetFileName(skeletonBytePath);
            data = null;
        }
        else
        {
            Debug.LogError("Skeleton binary file not found: " + skeletonBytePath);
            return;
        }
        Texture2D[] textures = new Texture2D[] { texture };
        runtimeAtlasAsset = SpineAtlasAsset.CreateRuntimeInstance(atlasText, textures, _baseMaterial, true);
        runtimeSkeletonDataAsset = SkeletonDataAsset.CreateRuntimeInstance(skeletonByte, runtimeAtlasAsset, true);
        runtimeSkeletonDataAsset.GetSkeletonData(false); // preload.
    }
}

