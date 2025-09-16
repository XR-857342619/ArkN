using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System;


public class TokenDownloadTool : MonoBehaviour
{
    public TextAsset characterDataJson; // 包含角色数据的JSON文件
    public string downloadDir = "DownloadedTokens/"; // 下载目录

    // 用于解析JSON的辅助类
    [System.Serializable]
    public class CharacterDataWrapper
    {
        public Dictionary<string, CharacterData> data;
    }

    [System.Serializable]
    public class CharacterData
    {
        public string characterPrefabKey;
        public Talent[] talents;
        // 其他字段不需要，所以不定义
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
        // 解析JSON数据
        CharacterDataWrapper wrapper = JsonUtility.FromJson<CharacterDataWrapper>("{\"data\":" + characterDataJson.text + "}");

        if (wrapper == null || wrapper.data == null)
        {
            Debug.LogError("Failed to parse JSON data");
            yield break;
        }

        // 收集所有tokenKey
        HashSet<string> tokenKeys = new HashSet<string>();

        foreach (var character in wrapper.data.Values)
        {
            if (character.talents != null)
            {
                foreach (var talent in character.talents)
                {
                    if (talent.candidates != null)
                    {
                        foreach (var candidate in talent.candidates)
                        {
                            if (!string.IsNullOrEmpty(candidate.tokenKey) &&
                                candidate.tokenKey.StartsWith("token_"))
                            {
                                tokenKeys.Add(candidate.tokenKey);
                                Debug.Log($"Found token: {candidate.tokenKey}");
                            }
                        }
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

        using (UnityEngine.Networking.UnityWebRequest webRequest = UnityEngine.Networking.UnityWebRequest.Get(url))
        {
            yield return webRequest.SendWebRequest();

            if (webRequest.result != UnityEngine.Networking.UnityWebRequest.Result.Success)
            {
                Debug.LogError($"Download failed for {tokenKey}: {webRequest.error}");
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