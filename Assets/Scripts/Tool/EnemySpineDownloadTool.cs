using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System;
using Newtonsoft.Json.Linq;
using UnityEngine.Networking;

public class EnemySpineDownloadTool : MonoBehaviour
{
    public TextAsset TextAsset;
    public string Dir;

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

        var root = JsonHelper.FromJson<Dictionary<string, object>>(TextAsset.text);
        if (root == null)
        {
            Debug.LogError("Failed to parse JSON");
            yield break;
        }

        // 收集所有敌人 ID（不归一化，PRTS 通常拥有全部变体的 spine 资源）
        HashSet<string> enemyIds = new HashSet<string>();

        if (root.TryGetValue("enemyData", out object enemyDataObj) && enemyDataObj is JObject enemyDataJObject)
        {
            foreach (var property in enemyDataJObject.Properties())
            {
                enemyIds.Add(property.Name);
            }
        }
        else if (root.TryGetValue("enemies", out object enemiesObj) && enemiesObj is JArray enemiesArray)
        {
            foreach (var item in enemiesArray)
            {
                if (item is JObject enemyObject && enemyObject.TryGetValue("Key", out JToken keyToken))
                {
                    enemyIds.Add(keyToken.ToString());
                }
            }
        }
        else
        {
            Debug.LogError("enemyData/enemies not found in JSON");
            yield break;
        }

        Debug.Log($"共发现 {enemyIds.Count} 个敌人 ID");

        foreach (string enemyId in enemyIds)
        {
            string targetDir = Dir + enemyId + "/";
            if (Directory.Exists(targetDir))
            {
                Debug.Log($"{targetDir} 已存在，跳过");
                continue;
            }

            Debug.Log($"开始爬取 {enemyId}");
            float t = Time.time;
            yield return StartCoroutine(DownloadOne(enemyId));
            Debug.Log($"{enemyId} 完成！耗时 {Time.time - t}");
        }

        Debug.Log("全部爬取完成！");
    }

    IEnumerator DownloadOne(string name)
    {
        List<Coroutine> coroutines = new List<Coroutine>();
        for (int i = 0; i < 3; i++)
        {
            coroutines.Add(StartCoroutine(Download(name, i)));
        }
        foreach (var co in coroutines)
        {
            yield return co;
        }
    }

    IEnumerator Download(string name, int type)
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

        string url = $"http://torappu.prts.wiki/assets/enemy_spine/{name}/{name}{end}";
        using (UnityWebRequest wr = UnityWebRequest.Get(url))
        {
            wr.timeout = 10;
            yield return wr.SendWebRequest();

            if (wr.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"Download Error {name}{end}: {wr.error}");
                yield break;
            }

            string path = Dir + name + "/";
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