using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System;

public class SpineDownLoadTool : MonoBehaviour
{
    public TextAsset TextAsset;
    public string Dir;
    public class A
    {
        public Dictionary<string, string[]> spCharGroups;
    }
    // Start is called before the first frame update
    void Start()
    {
        StartCoroutine(DownloadAll());
    }

    IEnumerator DownloadAll()
    {
        A a = JsonHelper.FromJson<A>(TextAsset.text);

        // 创建一个集合来存储所有需要下载的角色名，避免重复
        HashSet<string> allCharacters = new HashSet<string>();

        foreach (var kv in a.spCharGroups)
        {
            // 将键和值数组中的所有元素都添加到集合中
            allCharacters.Add(kv.Key);
            foreach (string character in kv.Value)
            {
                allCharacters.Add(character);
            }
        }

        // 遍历所有唯一的角色名进行下载
        foreach (string characterName in allCharacters)
        {
            string path = Dir + characterName;
            if (Directory.Exists(path))
            {
                Debug.Log(path + " 已存在");
                continue;
            }
            float t = Time.time;
            Debug.Log($"开始爬取{characterName}");
            yield return StartCoroutine(dowloadOne(characterName));
            Debug.Log($"{characterName}完成!耗时{Time.time - t}");
        }

        Debug.Log($"全部爬取完成！");
    }

    IEnumerator dowloadOne(string name)
    {
        List<Coroutine> coroutines = new List<Coroutine>();
        for (int i = 0; i < 3; i++)
        {
            coroutines.Add(StartCoroutine(Download(name, true, i)));
            coroutines.Add(StartCoroutine(Download(name, false, i)));
        }
        foreach (var co in coroutines)
        {
            yield return co;
        }
    }

    IEnumerator Download(string name,bool back,int type)
    {
        string end;
        switch (type)
        {
            case 0:
                end = ".png";
                break;
            case 1:
                end = ".skel";
                break;
            default:
                end = ".atlas";
                break;
        }
        UnityEngine.Networking.UnityWebRequest wr = UnityEngine.Networking.UnityWebRequest.Get("https://" + $"torappu.prts.wiki/assets/char_spine/{name}/defaultskin/{(back ? "back/" : "front/")}{name}{end}");
        Debug.Log("https://" + $"torappu.prts.wiki/assets/char_spine/{name}/defaultskin/{(back ? "back/" : "front/")}{name}{end}");
        yield return wr.SendWebRequest();
        if (!string.IsNullOrEmpty(wr.error))
        {
            Debug.LogError($"Download Error {name}  :" + wr.error);
        }
        else
        {
            string path = Dir + name + "/" + (back ? "back" : "front") + "/";
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);
            FileStream txt = new FileStream(path + $"{name}{end}", FileMode.Create);
            StreamWriter sw = new StreamWriter(txt);
            //Debug.Log(txt.Name);
            try
            {
                sw.BaseStream.Write(wr.downloadHandler.data, 0, wr.downloadHandler.data.Length);
            }
            catch (System.Exception e)
            {
                Debug.LogError(e);
            }
            sw.Close();
            txt.Close();
        }
    }
}
