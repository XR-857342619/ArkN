using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System;
using UnityEngine.Networking;

public class SpineDownLoadTool : MonoBehaviour
{
    public TextAsset TextAsset;
    public string Dir;

    public class A
    {
        public Dictionary<string, string[]> spCharGroups;
    }

    void Start()
    {
        StartCoroutine(DownloadAll());
    }

    IEnumerator DownloadAll()
    {
        if (TextAsset == null || string.IsNullOrEmpty(TextAsset.text))
        {
            Debug.LogError("TextAsset is null or empty");
            yield break;
        }

        A a = JsonHelper.FromJson<A>(TextAsset.text);
        if (a == null || a.spCharGroups == null)
        {
            Debug.LogError("spCharGroups not found in JSON");
            yield break;
        }

        // 创建集合存储所有需要下载的角色名，避免重复
        HashSet<string> allCharacters = new HashSet<string>();

        foreach (var kv in a.spCharGroups)
        {
            allCharacters.Add(kv.Key);
            if (kv.Value != null)
            {
                foreach (string character in kv.Value)
                {
                    if (!string.IsNullOrEmpty(character))
                        allCharacters.Add(character);
                }
            }
        }

        foreach (string characterName in allCharacters)
        {
            string path = Dir + characterName;
            if (Directory.Exists(path))
            {
                Debug.Log(path + " 已存在");
                continue;
            }

            float t = Time.time;
            Debug.Log($"开始爬取 {characterName}");
            yield return StartCoroutine(DownloadOne(characterName));
            Debug.Log($"{characterName} 完成!耗时{Time.time - t}");
        }

        Debug.Log("全部爬取完成！");
    }

    IEnumerator DownloadOne(string name)
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

    IEnumerator Download(string name, bool back, int type)
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

        string url = $"https://torappu.prts.wiki/assets/char_spine/{name}/defaultskin/{(back ? "back/" : "front/")}{name}{end}";
        Debug.Log(url);

        using (UnityWebRequest wr = UnityWebRequest.Get(url))
        {
            wr.timeout = 10;
            yield return wr.SendWebRequest();

            if (wr.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"Download Error {name}  :{wr.error}");
                yield break;
            }

            string path = Dir + name + "/" + (back ? "back" : "front") + "/";
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);

            try
            {
                File.WriteAllBytes(path + $"{name}{end}", wr.downloadHandler.data);
            }
            catch (Exception e)
            {
                Debug.LogError($"Save Error {name}{end}: {e}");
            }
        }
    }
}