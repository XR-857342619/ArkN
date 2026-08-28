using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System;
using UnityEngine.Networking;

public class TokenDownloadTool : MonoBehaviour
{
    public TextAsset characterDataJson; // 包含角色数据的JSON文件
    public string downloadDir = "DownloadedTokens/"; // 下载目录

    // 用于解析JSON的辅助类（JsonHelper/Newtonsoft 解析，支持 Dictionary）
    [System.Serializable]
    public class CharacterData
    {
        public string characterPrefabKey;
        public Talent[] talents;
    }

    [System.Serializable]
    public class Talent
    {
        public TalentCandidate[] candidates;
    }

    [System.Serializable]
    public class TalentCandidate
    {
        public string tokenKey;
    }

    void Start()
    {
        StartCoroutine(DownloadAllTokens());
    }

    IEnumerator DownloadAllTokens()
    {
        if (characterDataJson == null || string.IsNullOrEmpty(characterDataJson.text))
        {
            Debug.LogError("characterDataJson is null or empty");
            yield break;
        }

        // 使用 Newtonsoft 解析 character_table 的 Dictionary<string, CharacterData>
        Dictionary<string, CharacterData> data = JsonHelper.FromJson<Dictionary<string, CharacterData>>(characterDataJson.text);

        if (data == null || data.Count == 0)
        {
            Debug.LogError("Failed to parse character data JSON");
            yield break;
        }

        // 收集所有tokenKey
        HashSet<string> tokenKeys = new HashSet<string>();

        foreach (var character in data.Values)
        {
            if (character == null || character.talents == null)
                continue;

            foreach (var talent in character.talents)
            {
                if (talent == null || talent.candidates == null)
                    continue;

                foreach (var candidate in talent.candidates)
                {
                    if (candidate == null)
                        continue;

                    if (!string.IsNullOrEmpty(candidate.tokenKey) &&
                        candidate.tokenKey.StartsWith("token_"))
                    {
                        tokenKeys.Add(candidate.tokenKey);
                        Debug.Log($"Found token: {candidate.tokenKey}");
                    }
                }
            }
        }

        Debug.Log($"Found {tokenKeys.Count} unique tokens");

        // 下载所有token
        foreach (string tokenKey in tokenKeys)
        {
            string path = downloadDir + tokenKey;
            if (Directory.Exists(path))
            {
                Debug.Log($"{tokenKey} already exists, skipping");
                continue;
            }

            float startTime = Time.time;
            Debug.Log($"Starting download for {tokenKey}");
            yield return StartCoroutine(DownloadToken(tokenKey));
            Debug.Log($"{tokenKey} download completed! Time taken: {Time.time - startTime} seconds");
        }

        Debug.Log("All token downloads completed!");
    }

    IEnumerator DownloadToken(string tokenKey)
    {
        List<Coroutine> downloadCoroutines = new List<Coroutine>();

        // 下载正面和背面的三种文件类型
        for (int i = 0; i < 3; i++)
        {
            downloadCoroutines.Add(StartCoroutine(DownloadTokenFile(tokenKey, true, i)));
            downloadCoroutines.Add(StartCoroutine(DownloadTokenFile(tokenKey, false, i)));
        }

        // 等待所有下载完成
        foreach (var coroutine in downloadCoroutines)
        {
            yield return coroutine;
        }
    }

    IEnumerator DownloadTokenFile(string tokenKey, bool isBack, int fileType)
    {
        string fileExtension = GetFileExtension(fileType);
        string direction = isBack ? "back" : "front";

        string url = $"https://torappu.prts.wiki/assets/char_spine/{tokenKey}/defaultskin/{direction}/{tokenKey}{fileExtension}";
        Debug.Log($"Downloading from: {url}");

        using (UnityWebRequest webRequest = UnityWebRequest.Get(url))
        {
            webRequest.timeout = 10;
            yield return webRequest.SendWebRequest();

            if (webRequest.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"Download failed for {tokenKey} {direction} {fileExtension}: {webRequest.error}");
                yield break;
            }

            // 创建目录
            string directoryPath = Path.Combine(downloadDir, tokenKey, direction);
            if (!Directory.Exists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }

            // 保存文件
            string filePath = Path.Combine(directoryPath, $"{tokenKey}{fileExtension}");
            try
            {
                File.WriteAllBytes(filePath, webRequest.downloadHandler.data);
                Debug.Log($"Successfully saved: {filePath}");
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to save file {filePath}: {e.Message}");
            }
        }
    }

    string GetFileExtension(int fileType)
    {
        switch (fileType)
        {
            case 0: return ".png";
            case 1: return ".skel";
            case 2: return ".atlas";
            default: return ".png";
        }
    }
}