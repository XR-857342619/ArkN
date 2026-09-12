using System;
using System.Collections.Generic;
using System.Diagnostics;
using Debug = UnityEngine.Debug;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEngine.ResourceManagement.ResourceLocations;

public static class MergeCatalogTool
{
    const string TokenRuntimePath = "{UnityEngine.AddressableAssets.Addressables.RuntimePath}";
    const string ProviderAssetBundle = "UnityEngine.ResourceManagement.ResourceProviders.AssetBundleProvider";

    public static void InspectCatalogs(string b1Catalog, string b2Catalog)
    {
        if (string.IsNullOrEmpty(b1Catalog) || !File.Exists(b1Catalog))
        {
            Debug.LogError("[Merge] B1 catalog path is invalid or does not exist.");
            return;
        }
        if (string.IsNullOrEmpty(b2Catalog) || !File.Exists(b2Catalog))
        {
            Debug.LogError("[Merge] B2 catalog path is invalid or does not exist.");
            return;
        }

        var b1 = LoadCatalog(b1Catalog);
        var b2 = LoadCatalog(b2Catalog);
        var b1Entries = ExtractEntries(b1, "B1");
        var b2Entries = ExtractEntries(b2, "B2");

        Debug.Log($"[Merge] B1 entries={b1Entries.Count} B2 entries={b2Entries.Count}");
        Debug.Log($"[Merge] B1 providerIds={string.Join(",", b1.ProviderIds ?? new string[0])}");
        Debug.Log($"[Merge] B2 providerIds={string.Join(",", b2.ProviderIds ?? new string[0])}");

        var b1Internal = new HashSet<string>(b1Entries.Select(e => NormalizeBundleId(e.InternalId)));
        var b2Internal = new HashSet<string>(b2Entries.Select(e => NormalizeBundleId(e.InternalId)));
        Debug.Log($"[Merge] internal overlap={b1Internal.Intersect(b2Internal).Count()} b1Only={b1Internal.Except(b2Internal).Count()} b2Only={b2Internal.Except(b1Internal).Count()}");

        var b1Primary = new HashSet<string>(b1Entries.Select(e => e.Keys.Count > 0 ? e.Keys[0].ToString() : "<empty>"));
        var b2Primary = new HashSet<string>(b2Entries.Select(e => e.Keys.Count > 0 ? e.Keys[0].ToString() : "<empty>"));
        var commonPrimary = b1Primary.Intersect(b2Primary).OrderBy(x => x).ToList();
        Debug.Log($"[Merge] primary overlap={commonPrimary.Count} b1Only={b1Primary.Except(b2Primary).Count()} b2Only={b2Primary.Except(b1Primary).Count()}");
        Debug.Log("[Merge] commonPrimary sample:\n" + string.Join("\n", commonPrimary.Take(50)));
    }

    public static void MergeCatalogs(string b1Catalog, string b1BundleRoot, string b2Catalog, string b2BundleRoot, string outputCatalog, bool copyBundles)
    {

        // B2 Bundle Root 必须和 B2 catalog 同目录，避免 GUI 误填到 aa 上层目录。
        var b2CatalogDir = Path.GetDirectoryName(b2Catalog);
        if (!string.IsNullOrEmpty(b2CatalogDir) && Directory.Exists(b2CatalogDir))
            b2BundleRoot = b2CatalogDir;

        if (string.IsNullOrEmpty(b1Catalog) || !File.Exists(b1Catalog))
        {
            Debug.LogError("[Merge] B1 catalog path is invalid or does not exist.");
            return;
        }
        if (string.IsNullOrEmpty(b1BundleRoot) || !Directory.Exists(b1BundleRoot))
        {
            Debug.LogError("[Merge] B1 bundle root path is invalid or does not exist.");
            return;
        }
        if (string.IsNullOrEmpty(b2Catalog) || !File.Exists(b2Catalog))
        {
            Debug.LogError("[Merge] B2 catalog path is invalid or does not exist.");
            return;
        }
        if (string.IsNullOrEmpty(b2BundleRoot) || !Directory.Exists(b2BundleRoot))
        {
            Debug.LogError("[Merge] B2 bundle root path is invalid or does not exist.");
            return;
        }
        if (string.IsNullOrEmpty(outputCatalog))
            outputCatalog = b1Catalog;

        var outputDir = Path.GetDirectoryName(outputCatalog);
        if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
            Directory.CreateDirectory(outputDir);

        // 优先使用已验证的 PowerShell 原始二进制合并脚本。
        // Unity 原生 SetData 合并会重建 key/bucket 顺序，已验证会导致此项目资源加载异常。
        if (TryRunPowerShellMerge(b1Catalog, b1BundleRoot, b2Catalog, b2BundleRoot, outputCatalog, copyBundles))
            return;

        var b1 = LoadCatalog(b1Catalog);
        var b2 = LoadCatalog(b2Catalog);
        var b1Entries = ExtractEntries(b1, "B1");
        var b2Entries = ExtractEntries(b2, "B2");

        var b1Addresses = new HashSet<string>();
        var b1BundleInternalIds = new HashSet<string>();
        var b1BundleLogical = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var e in b1Entries)
        {
            if (e.Provider == ProviderAssetBundle)
            {
                b1BundleInternalIds.Add(NormalizeBundleId(e.InternalId));
                var logical = GetBundleLogicalName(e.InternalId);
                if (!b1BundleLogical.TryGetValue(logical, out var list))
                    b1BundleLogical[logical] = list = new List<string>();
                list.Add(GetBundleKey(e.InternalId));
            }
            else if (e.Keys.Count > 0 && e.Keys[0] is string s)
            {
                b1Addresses.Add(s);
            }
        }

        // B2 中与 B1 逻辑同名但 hash 不同的 bundle，统一重定向到 B1 对应 bundle。
        var b2BundleLogical = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var e in b2Entries)
        {
            if (e.Provider != ProviderAssetBundle) continue;
            var logical = GetBundleLogicalName(e.InternalId);
            if (!b2BundleLogical.TryGetValue(logical, out var list))
                b2BundleLogical[logical] = list = new List<string>();
            list.Add(GetBundleKey(e.InternalId));
        }

        var keyRemap = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var kvp in b2BundleLogical)
        {
            if (!b1BundleLogical.TryGetValue(kvp.Key, out var b1Keys)) continue;
            var b1Key = b1Keys[0];
            foreach (var b2Key in kvp.Value)
            {
                if (b2Key != b1Key)
                    keyRemap[b2Key] = b1Key;
            }
        }

        var mergedEntries = new List<ContentCatalogDataEntry>(b1Entries);
        var b2BundleInternalIds = new HashSet<string>(
            b2Entries.Where(e => e.Provider == ProviderAssetBundle).Select(e => e.InternalId));
        int skippedAddress = 0;
        int skippedDuplicateBundle = 0;
        int addedB2 = 0;

        foreach (var e in b2Entries)
        {
            bool isBundleEntry = e.Provider == ProviderAssetBundle;
            if (isBundleEntry)
            {
                var bundleKey = GetBundleKey(e.InternalId);
                if (b1BundleInternalIds.Contains(NormalizeBundleId(e.InternalId)) ||
                    keyRemap.ContainsKey(bundleKey))
                {
                    skippedDuplicateBundle++;
                    continue;
                }
            }
            else
            {
                if (e.Keys.Count > 0 && e.Keys[0] is string s && b1Addresses.Contains(s))
                {
                    skippedAddress++;
                    continue;
                }
            }

            // 将 B2 资源对重叠 B2 bundle 的依赖改指向 B1 bundle。
            for (int i = 0; i < e.Dependencies.Count; i++)
            {
                if (e.Dependencies[i] is string dep && keyRemap.TryGetValue(dep, out var mapped))
                    e.Dependencies[i] = mapped;
            }

            mergedEntries.Add(e);
            addedB2++;
        }

        var mergedData = new ContentCatalogData(mergedEntries, b1.ProviderId);
        mergedData.InstanceProviderData = b1.InstanceProviderData;
        mergedData.SceneProviderData = b1.SceneProviderData;
        mergedData.ResourceProviderData = b1.ResourceProviderData;

        var json = JsonUtility.ToJson(mergedData);
        File.WriteAllText(outputCatalog, json, new System.Text.UTF8Encoding(false));

        Debug.Log($"[Merge] merged catalog written: {outputCatalog}");
        Debug.Log($"[Merge] B1 entries={b1Entries.Count} B2 entries={b2Entries.Count} addedB2={addedB2} skippedAddress={skippedAddress} skippedDuplicateBundle={skippedDuplicateBundle} total={mergedEntries.Count}");

        if (copyBundles)
        {
            // 复制最终 merged catalog 中实际引用的 B2 bundle。
            // 不依赖“本次新增”，这样在重复执行/已合并过的 catalog 上也能补回缺失 bundle。
            var mergedBundleInternalIds = new HashSet<string>(
                mergedEntries.Where(e => e.Provider == ProviderAssetBundle).Select(e => e.InternalId));
            var b2BundlesToCopy = new HashSet<string>(
                b2BundleInternalIds.Where(id => mergedBundleInternalIds.Contains(id)));
            CopyB2Bundles(b1BundleRoot, b2BundleRoot, b2BundlesToCopy);
        }
        else
            Debug.Log("[Merge] bundle copy skipped.");
    }

    public static void GenerateBranch1ExtraCatalog(
        string mainCatalog,
        string mainBundleRoot,
        string extraSourceCatalog,
        string extraSourceBundleRoot,
        string outputExtraCatalog,
        string outputBundleRoot,
        string outputAddressList)
    {

        // 自动按 catalog 所在目录推导 bundle 根目录，避免 GUI 误填。
        var mainCatalogDir = Path.GetDirectoryName(mainCatalog);
        if (!string.IsNullOrEmpty(mainCatalogDir) && Directory.Exists(mainCatalogDir))
            mainBundleRoot = mainCatalogDir;

        var extraSourceCatalogDir = Path.GetDirectoryName(extraSourceCatalog);
        if (!string.IsNullOrEmpty(extraSourceCatalogDir) && Directory.Exists(extraSourceCatalogDir))
            extraSourceBundleRoot = extraSourceCatalogDir;

        if (string.IsNullOrEmpty(mainCatalog) || !File.Exists(mainCatalog))
        {
            Debug.LogError("[Extra] Main catalog path is invalid or does not exist.");
            return;
        }
        if (string.IsNullOrEmpty(extraSourceCatalog) || !File.Exists(extraSourceCatalog))
        {
            Debug.LogError("[Extra] Extra source catalog path is invalid or does not exist.");
            return;
        }
        if (string.IsNullOrEmpty(outputExtraCatalog))
            outputExtraCatalog = Path.Combine(mainBundleRoot, "extra_catalog.json");
        if (string.IsNullOrEmpty(outputBundleRoot))
            outputBundleRoot = mainBundleRoot;

        var outputDir = Path.GetDirectoryName(outputExtraCatalog);
        if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
            Directory.CreateDirectory(outputDir);

        var main = LoadCatalog(mainCatalog);
        var source = LoadCatalog(extraSourceCatalog);
        var mainEntries = ExtractEntries(main, "Main");
        var sourceEntries = ExtractEntries(source, "ExtraSource");

        var mainAddresses = new HashSet<string>();
        var mainBundleInternalIds = new HashSet<string>();
        var mainBundleLogical = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var mainBundleEntriesByKey = new Dictionary<string, ContentCatalogDataEntry>(StringComparer.Ordinal);
        foreach (var e in mainEntries)
        {
            if (e.Provider == ProviderAssetBundle)
            {
                mainBundleInternalIds.Add(NormalizeBundleId(e.InternalId));
                var logical = GetBundleLogicalName(e.InternalId);
                if (!mainBundleLogical.TryGetValue(logical, out var list))
                    mainBundleLogical[logical] = list = new List<string>();
                list.Add(GetBundleKey(e.InternalId));

                var bundleKey = GetBundleKey(e.InternalId);
                if (!mainBundleEntriesByKey.ContainsKey(bundleKey))
                    mainBundleEntriesByKey[bundleKey] = e;
            }
            else if (e.Keys.Count > 0 && e.Keys[0] is string s)
            {
                mainAddresses.Add(s);
            }
        }

        var sourceBundleLogical = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var e in sourceEntries)
        {
            if (e.Provider != ProviderAssetBundle) continue;
            var logical = GetBundleLogicalName(e.InternalId);
            if (!sourceBundleLogical.TryGetValue(logical, out var list))
                sourceBundleLogical[logical] = list = new List<string>();
            list.Add(GetBundleKey(e.InternalId));
        }

        var keyRemap = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var kvp in sourceBundleLogical)
        {
            if (!mainBundleLogical.TryGetValue(kvp.Key, out var mainKeys)) continue;
            var mainKey = mainKeys[0];
            foreach (var sourceKey in kvp.Value)
            {
                if (sourceKey != mainKey)
                    keyRemap[sourceKey] = mainKey;
            }
        }

        var mergedEntries = new List<ContentCatalogDataEntry>();
        var requiredMainBundleKeys = new HashSet<string>();
        var addedBundleInternalIds = new HashSet<string>();
        var addressSet = new HashSet<string>();
        int skippedAddress = 0;
        int skippedBundle = 0;

        foreach (var e in sourceEntries)
        {
            bool isBundleEntry = e.Provider == ProviderAssetBundle;
            if (isBundleEntry)
            {
                var bundleKey = GetBundleKey(e.InternalId);
                if (mainBundleInternalIds.Contains(NormalizeBundleId(e.InternalId)) ||
                    keyRemap.ContainsKey(bundleKey))
                {
                    skippedBundle++;
                    continue;
                }
                addedBundleInternalIds.Add(e.InternalId);
            }
            else
            {
                if (e.Keys.Count > 0 && e.Keys[0] is string s && mainAddresses.Contains(s))
                {
                    skippedAddress++;
                    continue;
                }
                if (e.Keys.Count > 0 && e.Keys[0] is string addr)
                    addressSet.Add(addr);
            }

            for (int i = 0; i < e.Dependencies.Count; i++)
            {
                if (e.Dependencies[i] is string dep)
                {
                    if (keyRemap.TryGetValue(dep, out var mapped))
                    {
                        e.Dependencies[i] = mapped;
                        requiredMainBundleKeys.Add(mapped);
                    }
                    else if (mainBundleEntriesByKey.ContainsKey(dep))
                    {
                        requiredMainBundleKeys.Add(dep);
                    }
                }
            }

            mergedEntries.Add(e);
        }

        // 额外 catalog 中必须包含 branch2 的 bundle stub，否则 SetData 无法解析依赖 key。
        var stubKeys = new HashSet<string>();
        var stubQueue = new Queue<string>(requiredMainBundleKeys);
        while (stubQueue.Count > 0)
        {
            var key = stubQueue.Dequeue();
            if (!stubKeys.Add(key)) continue;
            if (!mainBundleEntriesByKey.TryGetValue(key, out var stub)) continue;

            mergedEntries.Add(stub);
            foreach (var depObj in stub.Dependencies)
            {
                if (depObj is string depKey)
                    stubQueue.Enqueue(depKey);
            }
        }

        var data = new ContentCatalogData(mergedEntries, "Branch1ExtraContentCatalog");
        data.InstanceProviderData = main.InstanceProviderData;
        data.SceneProviderData = main.SceneProviderData;
        data.ResourceProviderData = main.ResourceProviderData;

        var json = JsonUtility.ToJson(data);
        File.WriteAllText(outputExtraCatalog, json, new System.Text.UTF8Encoding(false));
        File.WriteAllLines(outputAddressList, addressSet.OrderBy(x => x, StringComparer.Ordinal), new System.Text.UTF8Encoding(false));

        Debug.Log($"[Extra] extra catalog written: {outputExtraCatalog}");
        Debug.Log($"[Extra] added={mergedEntries.Count} skippedAddress={skippedAddress} skippedBundle={skippedBundle} addresses={addressSet.Count}");

        CopyExtraBundles(extraSourceBundleRoot, outputBundleRoot, addedBundleInternalIds);
    }

    static void CopyB2Bundles(string b1BundleRoot, string b2BundleRoot, HashSet<string> bundleInternalIds)
    {
        CopyBundles(b2BundleRoot, b1BundleRoot, bundleInternalIds, "[Merge]");
    }

    static void CopyExtraBundles(string sourceBundleRoot, string outputBundleRoot, HashSet<string> bundleInternalIds)
    {
        CopyBundles(sourceBundleRoot, outputBundleRoot, bundleInternalIds, "[Extra]");
    }

    static void CopyBundles(string sourceBundleRoot, string outputBundleRoot, HashSet<string> bundleInternalIds, string logPrefix)
    {
        if (bundleInternalIds == null || bundleInternalIds.Count == 0)
        {
            Debug.Log($"{logPrefix} bundle copy skipped: no unique bundles to copy.");
            return;
        }

        var platformFolder = GetPlatformFolder(bundleInternalIds.First());
        var srcBase = Path.Combine(sourceBundleRoot, platformFolder);
        var dstBase = Path.Combine(outputBundleRoot, platformFolder);
        int copied = 0;
        int skippedSame = 0;
        int conflictDifferent = 0;
        int missing = 0;

        foreach (var internalId in bundleInternalIds)
        {
            var rel = GetBundleKey(internalId).Replace('/', Path.DirectorySeparatorChar);
            var src = Path.Combine(srcBase, rel);
            var dst = Path.Combine(dstBase, rel);

            if (!File.Exists(src))
            {
                Debug.LogWarning($"{logPrefix} Missing source bundle: {rel}");
                missing++;
                continue;
            }

            var dstDir = Path.GetDirectoryName(dst);
            if (!string.IsNullOrEmpty(dstDir))
                Directory.CreateDirectory(dstDir);

            if (File.Exists(dst))
            {
                if (FilesEqual(src, dst))
                {
                    skippedSame++;
                    continue;
                }

                Debug.LogWarning($"{logPrefix} Conflicting bundle with different content: {rel}");
                conflictDifferent++;
                continue;
            }

            File.Copy(src, dst);
            copied++;
        }

        Debug.Log($"{logPrefix} bundle copy done: copied={copied} skippedSame={skippedSame} conflictsDifferent={conflictDifferent} missing={missing}");
    }

    static bool TryRunPowerShellMerge(string b1Catalog, string b1BundleRoot, string b2Catalog, string b2BundleRoot, string outputCatalog, bool copyBundles)
    {
#if UNITY_EDITOR_WIN
        try
        {
            var projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            if (string.IsNullOrEmpty(projectRoot))
                return false;

            var scriptPath = Path.Combine(projectRoot, "Tools", "merge_catalogs.ps1");
            if (!File.Exists(scriptPath))
            {
                Debug.LogWarning($"[Merge] PowerShell merge script not found: {scriptPath}");
                return false;
            }

            string Q(string s) => "\"" + (s ?? string.Empty).Replace("\"", "\\\"") + "\"";
            var args = $"-NoProfile -ExecutionPolicy Bypass -File {Q(scriptPath)} " +
                       $"-B1Catalog {Q(b1Catalog)} -B1BundleRoot {Q(b1BundleRoot)} " +
                       $"-B2Catalog {Q(b2Catalog)} -B2BundleRoot {Q(b2BundleRoot)} " +
                       $"-OutputCatalog {Q(outputCatalog)}";
            if (!copyBundles)
                args += " -CopyBundles:$false";

            var psi = new ProcessStartInfo("powershell.exe", args)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using (var process = Process.Start(psi))
            {
                if (process == null)
                    return false;

                var stdout = process.StandardOutput.ReadToEnd();
                var stderr = process.StandardError.ReadToEnd();
                process.WaitForExit();

                if (!string.IsNullOrEmpty(stdout))
                    Debug.Log("[Merge][PowerShell] " + stdout.Trim());
                if (!string.IsNullOrEmpty(stderr))
                    Debug.LogWarning("[Merge][PowerShell] " + stderr.Trim());

                if (process.ExitCode != 0)
                {
                    Debug.LogError($"[Merge] PowerShell merge failed with exit code {process.ExitCode}.");
                    return false;
                }
            }

            return true;
        }
        catch (Exception e)
        {
            Debug.LogWarning("[Merge] PowerShell merge failed, fallback to native merge. " + e);
            return false;
        }
#else
        return false;
#endif
    }
    static bool FilesEqual(string pathA, string pathB)
    {
        using (var md5 = System.Security.Cryptography.MD5.Create())
        {
            using (var a = File.OpenRead(pathA))
            using (var b = File.OpenRead(pathB))
            {
                byte[] ha = md5.ComputeHash(a);
                byte[] hb = md5.ComputeHash(b);
                return Convert.ToBase64String(ha) == Convert.ToBase64String(hb);
            }
        }
    }

    static ContentCatalogData LoadCatalog(string path)
    {
        var json = File.ReadAllText(path);
        return JsonUtility.FromJson<ContentCatalogData>(json);
    }

    static List<ContentCatalogDataEntry> ExtractEntries(ContentCatalogData data, string label)
    {
        var locator = data.CreateLocator();
        var locToKeys = new Dictionary<IResourceLocation, List<object>>();

        foreach (var kvp in locator.Locations)
        {
            if (kvp.Value == null) continue;
            foreach (var loc in kvp.Value)
            {
                if (loc == null) continue;
                List<object> keys;
                if (!locToKeys.TryGetValue(loc, out keys))
                    locToKeys[loc] = keys = new List<object>();
                if (!keys.Contains(kvp.Key))
                    keys.Add(kvp.Key);
            }
        }

        var result = new List<ContentCatalogDataEntry>();
        foreach (var kvp in locToKeys)
        {
            var loc = kvp.Key;
            var keys = kvp.Value;

            if (!string.IsNullOrEmpty(loc.PrimaryKey))
            {
                keys.Remove(loc.PrimaryKey);
                keys.Insert(0, loc.PrimaryKey);
            }

            var deps = new List<object>();
            if (loc.HasDependencies && loc.Dependencies != null)
            {
                foreach (var dep in loc.Dependencies)
                    deps.Add(dep.PrimaryKey);
            }

            var internalId = ToTemplateInternalId(loc.InternalId);
            result.Add(new ContentCatalogDataEntry(loc.ResourceType, internalId, loc.ProviderId, keys, deps, loc.Data));
        }

        Debug.Log($"[Merge] {label}: locator keys={locator.Locations.Count} unique locations={result.Count} providerIds={string.Join(",", data.ProviderIds ?? new string[0])}");
        return result;
    }

    static string ToTemplateInternalId(string id)
    {
        try
        {
            var rt = Addressables.RuntimePath;
            if (!string.IsNullOrEmpty(rt))
            {
                if (id.StartsWith(rt + "\\", StringComparison.Ordinal))
                    return TokenRuntimePath + id.Substring(rt.Length);
                if (id.StartsWith(rt + "/", StringComparison.Ordinal))
                    return TokenRuntimePath + id.Substring(rt.Length);
                var normalizedRt = rt.Replace('\\', '/');
                var normalizedId = id.Replace('\\', '/');
                if (normalizedId.StartsWith(normalizedRt + "/", StringComparison.Ordinal))
                    return TokenRuntimePath + "/" + normalizedId.Substring(normalizedRt.Length + 1);
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning("[Merge] ToTemplateInternalId failed: " + e);
        }
        return id;
    }

    static string GetBundleKey(string internalId)
    {
        int tokenIdx = internalId.IndexOf(TokenRuntimePath, StringComparison.Ordinal);
        if (tokenIdx >= 0)
        {
            int start = tokenIdx + TokenRuntimePath.Length;
            while (start < internalId.Length && (internalId[start] == '/' || internalId[start] == '\\'))
                start++;
            int sep = internalId.IndexOfAny(new[] { '/', '\\' }, start);
            if (sep >= 0)
                return internalId.Substring(sep + 1);
        }

        return internalId;
    }

    static string GetPlatformFolder(string internalId)
    {
        int tokenIdx = internalId.IndexOf(TokenRuntimePath, StringComparison.Ordinal);
        if (tokenIdx >= 0)
        {
            int start = tokenIdx + TokenRuntimePath.Length;
            while (start < internalId.Length && (internalId[start] == '/' || internalId[start] == '\\'))
                start++;
            int sep = internalId.IndexOfAny(new[] { '/', '\\' }, start);
            if (sep > start)
                return internalId.Substring(start, sep - start);
        }

        return "StandaloneWindows64";
    }

    static string GetBundleLogicalName(string internalId)
    {
        var key = GetBundleKey(internalId).Replace('/', '\\');
        var name = Path.GetFileNameWithoutExtension(key);
        int underscore = name.LastIndexOf('_');
        if (underscore > 0 && underscore + 33 == name.Length)
        {
            var hashPart = name.Substring(underscore + 1);
            if (hashPart.Length == 32 && hashPart.All(c => Uri.IsHexDigit(c)))
                return name.Substring(0, underscore);
        }
        return name;
    }

    static string NormalizeBundleId(string internalId)
    {
        return internalId.Replace('\\', '/');
    }
}