using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System.Linq;
using UnityEngine.Networking;

public class HalfDownLoadTool : MonoBehaviour
{
    public TextAsset PartText, CharText;
    public string IconDir, HalfDir;
    public string[] Targets;

    public class PrtsInfo
    {
        public string cn;
        public string en;
        public string icon;
        public string half;
    }

    public class CharInfo
    {
        public string name;
    }

    void Start()
    {
        StartCoroutine(DownloadAll());
    }

    IEnumerator DownloadAll()
    {
        if (PartText == null || CharText == null)
        {
            Debug.LogError("PartText or CharText is null");
            yield break;
        }

        PrtsInfo[] partInfos = JsonHelper.FromJson<PrtsInfo[]>(PartText.text);
        Dictionary<string, CharInfo> charInfos = JsonHelper.FromJson<Dictionary<string, CharInfo>>(CharText.text);

        if (partInfos == null || charInfos == null)
        {
            Debug.LogError("Failed to parse PartText or CharText");
            yield break;
        }

        string getIdByName(string name)
        {
            if (string.IsNullOrEmpty(name) || charInfos == null)
                return "";

            var pair = charInfos.FirstOrDefault(x => x.Value != null && x.Value.name == name);
            if (pair.Value == null || string.IsNullOrEmpty(pair.Key))
                return "";

            var ss = pair.Key.Split('_');
            return ss.Length > 0 ? ss.Last() : "";
        }

        foreach (var kv in partInfos)
        {
            if (kv == null || string.IsNullOrEmpty(kv.cn))
                continue;

            if (Targets != null && Targets.Length > 0 && !Targets.Contains(kv.cn))
                continue;

            float t = Time.time;
            Debug.Log($"开始爬取 {kv.cn}");

            string id = getIdByName(kv.cn);
            if (string.IsNullOrEmpty(id))
            {
                Debug.LogWarning($"未在 character_table 中找到 {kv.cn} 对应的 ID，跳过");
                continue;
            }

            yield return StartCoroutine(Download(kv.icon, "icon_" + id, IconDir));
            yield return StartCoroutine(Download(kv.half, "half_" + id, HalfDir));
            Debug.Log($"{kv.cn} 完成!耗时{Time.time - t}");
        }

        Debug.Log("全部爬取完成！");
    }

    IEnumerator Download(string url, string name, string dir)
    {
        if (string.IsNullOrEmpty(url))
        {
            Debug.LogWarning($"下载地址为空: {name}");
            yield break;
        }

        int index = url.IndexOf("110px-");
        if (index > 0)
        {
            url = url.Substring(0, index - 1);
            url = url.Replace("thumb/", "");
        }

        using (UnityWebRequest wr = UnityWebRequest.Get(url))
        {
            wr.timeout = 10;
            yield return wr.SendWebRequest();

            if (wr.result != UnityWebRequest.Result.Success)
            {
                Debug.Log("Download Error:" + wr.error);
                yield break;
            }

            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            try
            {
                File.WriteAllBytes(dir + $"{name.ToLower()}.png", wr.downloadHandler.data);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Save Error: {e}");
            }
        }
    }
}