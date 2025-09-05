using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

public class SaveHelper
{
    public const string SingleSaveKey = "SingleData";

    public static string LoadBaseFile(string fileName)
    {
        try
        {
            var str = ResHelper.GetAsset<TextAsset>("Assets/Bundles/Data/Map/Main/" + fileName).text;
            return str;
        }
        catch (Exception e)
        {
            return string.Empty;
        }
        return string.Empty;
    }

    public static void SaveData()
    {
        string str = JsonHelper.ToJsonWithType(GameData.Instance);
        string fileName = "/data";
        var dir = System.IO.Path.GetDirectoryName(PathHelper.AppHotfixResPath + fileName) + "/";
        fileName = System.IO.Path.GetFileNameWithoutExtension(fileName) + ".sav";
        //var fileName = (errorSave ? "error_" : "") + System.DateTime.Now.ToString("yyyy-MM-dd-HHmmss") + ".txt";
        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }
        FileStream txt = new FileStream(dir + fileName, FileMode.Create);
        StreamWriter sw = new StreamWriter(txt);
        sw.Write(str);
        sw.Close();
        txt.Close();
    }
    public static void SavePackageConfig(string packageName, Config config)
    {
        string str = JsonHelper.ToJson(config);
        string path = PathHelper.MapResPath + "/Map/" + packageName + "/config.ini";
        FileStream txt = new FileStream(path, FileMode.Create);
        StreamWriter sw = new StreamWriter(txt);
        sw.Write(str);
        sw.Close();
    }
    public static string LoadConfig(string packageName)
    {
        string path = PathHelper.MapResPath + "/Map/" + packageName + "/config.ini";
        using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read))
        {
            byte[] bytes = new byte[stream.Length];
            stream.Read(bytes, 0, (int)stream.Length);
            stream.Close();
            return Encoding.UTF8.GetString(bytes);
        }
    }
    public static string LoadFile(string fileName)
    {
        string filePath = PathHelper.AppHotfixResPath + fileName;
        //Debug.Log(filePath);
        if (!File.Exists(filePath))
        {
            return string.Empty;
        }

        try
        {
            using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read))
            {
                byte[] bytes = new byte[stream.Length];
                stream.Read(bytes, 0, (int)stream.Length);
                stream.Close();
                return Encoding.UTF8.GetString(bytes);
            }
        }
        catch (Exception e)
        {
            Debug.LogException(e);
        }

        return string.Empty;
    }
    public static string LoadMap(string fileName)
    {
        string filePath = PathHelper.MapResPath + fileName;
        if (!File.Exists(filePath))
        {
            return string.Empty;
        }

        try
        {
            using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read))
            {
                byte[] bytes = new byte[stream.Length];
                stream.Read(bytes, 0, (int)stream.Length);
                stream.Close();
                return Encoding.UTF8.GetString(bytes);
            }
        }
        catch (Exception e)
        {
            Debug.LogException(e);
        }

        return string.Empty;
    }

    public static void DeleteFile(string fileName)
    {
        string filePath = PathHelper.AppHotfixResPath + fileName;
        if (File.Exists(filePath))
        {
            System.IO.File.Delete(filePath);
        }
    }
    public static bool ExistFile(string packageName, string fileName)
    {
#if UNITY_EDITOR
        var dir = Directory.GetCurrentDirectory() + "/Assets/Bundles/Data/Map/" + packageName + "/";
        fileName = System.IO.Path.GetFileNameWithoutExtension(fileName) + ".txt";
#else
        var dir = System.IO.Path.GetDirectoryName(PathHelper.AppHotfixResPath + "/Map/" + packageName)+"/";
         fileName = System.IO.Path.GetFileNameWithoutExtension(fileName) + ".map";
#endif
        return File.Exists(dir + fileName);
    }

    public static string Save(string str, string packageName, string fileName)
    {
#if UNITY_EDITOR
        var dir = Directory.GetCurrentDirectory() + "/Assets/Bundles/Data/Map/" + packageName + "/";
        fileName = System.IO.Path.GetFileNameWithoutExtension(fileName) + ".txt";
#else
        var dir = System.IO.Path.GetDirectoryName(PathHelper.AppHotfixResPath + fileName)+"/";
         fileName = System.IO.Path.GetFileNameWithoutExtension(fileName) + ".map";
#endif
        //var fileName = (errorSave ? "error_" : "") + System.DateTime.Now.ToString("yyyy-MM-dd-HHmmss") + ".txt";
        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }
        FileStream txt = new FileStream(dir + fileName, FileMode.Create);
        StreamWriter sw = new StreamWriter(txt);
        sw.Write(str);
        sw.Close();
        txt.Close();
#if UNITY_EDITOR
        UnityEditor.AssetDatabase.Refresh();
#endif
        return dir + fileName;
    }
}