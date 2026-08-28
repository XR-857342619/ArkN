using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System.Linq;
using UnityEngine.Networking;

public class StandPicDownLoadTool : MonoBehaviour
{
    public TextAsset PartText, CharText;
    public string Dir;
    public string StartName;
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

        bool needContinue = !string.IsNullOrEmpty(StartName);
        foreach (var kv in partInfos)
        {
            if (kv == null || string.IsNullOrEmpty(kv.cn))
                continue;

            if (needContinue)
            {
                if (kv.cn != StartName)
                    continue;
                needContinue = false;
            }

            if (Targets != null && Targets.Length > 0 && !Targets.Contains(kv.cn))
                continue;

            float t = Time.time;
            Debug.Log($"开始爬取 {kv.cn}");

            string fileName = getIdByName(kv.cn);
            if (string.IsNullOrEmpty(fileName))
            {
                Debug.LogWarning($"未在 character_table 中找到 {kv.cn} 对应的 ID，跳过");
                continue;
            }

            yield return StartCoroutine(Download(fileName, kv.cn, Dir));
            Debug.Log($"{kv.cn} 完成!耗时{Time.time - t}");
        }

        Debug.Log("全部爬取完成！");
    }

    IEnumerator Download(string fileName, string name, string dir)
    {
        string url = $"http://prts.wiki/w/" + UnityWebRequest.EscapeURL($"文件:立绘_{name}_1").ToUpper() + ".png";
        Debug.Log(url);

        using (UnityWebRequest wr = UnityWebRequest.Get(url))
        {
            wr.timeout = 10;
            yield return wr.SendWebRequest();

            if (wr.result != UnityWebRequest.Result.Success)
            {
                Debug.Log("Download Error:" + wr.error);
                yield break;
            }

            var htmlText = wr.downloadHandler.text;
            int startIndex = htmlText.IndexOf(UnityWebRequest.EscapeURL($"立绘_{name}_1").ToUpper());
            if (startIndex < 6)
            {
                Debug.LogWarning($"未在页面中找到立绘_{name}_1，跳过下载");
                yield break;
            }

            var next = htmlText.Substring(startIndex - 6, 6);
            var nextUrl = $"http://prts.wiki/images" + next + UnityWebRequest.EscapeURL($"立绘_{name}_1").ToUpper() + ".png";
            Debug.Log(nextUrl);

            using (UnityWebRequest imageRequest = UnityWebRequest.Get(nextUrl))
            {
                imageRequest.timeout = 10;
                yield return imageRequest.SendWebRequest();

                if (imageRequest.result != UnityWebRequest.Result.Success)
                {
                    Debug.Log("Download Error:" + imageRequest.error);
                    yield break;
                }

                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                try
                {
                    File.WriteAllBytes(dir + $"{fileName.ToLower()}.png", imageRequest.downloadHandler.data);
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"Save Error: {e}");
                }
            }
        }
    }
}