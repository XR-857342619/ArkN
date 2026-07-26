using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System;
using Newtonsoft.Json.Linq;

public class EnemySpineDownloadTool : MonoBehaviour
{
    public TextAsset TextAsset;
    public string Dir;

    // Start is called before the first frame update
    void Start()
    {
        StartCoroutine(DownloadAll());
    }

    IEnumerator DownloadAll()
    {
        var root = JsonHelper.FromJson<Dictionary<string, object>>(TextAsset.text);
        if (root == null)
        {
            Debug.LogError("Failed to parse JSON");
            yield break;
        }

        if (!root.TryGetValue("enemyData", out object enemyDataObj))
        {
            Debug.LogError("enemyData not found in JSON");
            yield break;
        }

        var enemyDataJObject = enemyDataObj as JObject;
        if (enemyDataJObject == null)
        {
            Debug.LogError("enemyData is not a JObject");
            yield break;
        }

        foreach (var property in enemyDataJObject.Properties())
        {
            string enemyId = property.Name;
            Debug.Log($"开始爬取 {enemyId}");
            float t = Time.time;
            yield return StartCoroutine(dowloadOne(enemyId));
            Debug.Log($"{enemyId} 完成！耗时 {Time.time - t}");
        }

        Debug.Log("全部爬取完成！");
    }

    IEnumerator dowloadOne(string name)
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
        UnityEngine.Networking.UnityWebRequest wr = UnityEngine.Networking.UnityWebRequest.Get("http://" + $"torappu.prts.wiki/assets/enemy_spine/{name}/{name}{end}");
        //https://torappu.prts.wiki/assets/enemy_spine/enemy_8010_mcnist/enemy_8010_mcnist.png
        wr.timeout = 2;
        yield return wr.SendWebRequest();
        if (!string.IsNullOrEmpty(wr.error))
        {
            Debug.LogError($"Download Error {name}:" + wr.error);
            try
            {
                Debug.LogWarning(Database.Instance.Get<UnitData>(name).Name);
            }
            catch (Exception e)
            {
                Debug.LogError($"not fond {name}");
            }
        }
        else
        {
            string path = Dir + name + "/";
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
