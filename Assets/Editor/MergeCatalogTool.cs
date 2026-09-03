using System;
using System.Collections.Generic;
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

        var b1Internal = new HashSet<string>(b1Entries.Select(e => NormalizeBundleId(e.InternalId)));
        var b2Internal = new HashSet<string>(b2Entries.Select(e => NormalizeBundleId(e.InternalId)));
        Debug.Log($"[Merge] internal overlap={b1Internal.Intersect(b2Internal).Count()} b1Only={b1Internal.Except(b2Internal).Count()} b2Only={b2Internal.Except(b1Internal).Count()}");

        var b1Primary = new HashSet<string>(b1Entries.Select(e => e.Keys.Count > 0 ? e.Keys[0].ToString() : "<empty>"));
        var b2Primary = new HashSet<string>(b2Entries.Select(e => e.Keys.Count > 0 ? e.Keys[0].ToString() : "<empty>"));
        var commonPrimary = b1Primary.Intersect(b2Primary).OrderBy(x => x).ToList();
        Debug.Log($"[Merge] primary overlap={commonPrimary.Count} b1Only={b1Primary.Except(b2Primary).Count()} b2Only={b2Primary.Except(b1Primary).Count()}");
        Debug.Log("[Merge] commonPrimary sample:\n" + string.Join("\n", commonPrimary.Take(50)));

        var b1AllKeys = new HashSet<string>(b1Entries.SelectMany(e => e.Keys).Select(k => k.ToString()));
        var b2AllKeys = new HashSet<string>(b2Entries.SelectMany(e => e.Keys).Select(k => k.ToString()));
        Debug.Log($"[Merge] all-key overlap={b1AllKeys.Intersect(b2AllKeys).Count()} b1AllKeys={b1AllKeys.Count} b2AllKeys={b2AllKeys.Count}");
    }

    public static void MergeCatalogs(string b1Catalog, string b1BundleRoot, string b2Catalog, string b2BundleRoot, string outputCatalog, bool copyBundles)
    {
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

        const string providerAssetBundle = "UnityEngine.ResourceManagement.ResourceProviders.AssetBundleProvider";

        var b1 = LoadCatalog(b1Catalog);
        var b2 = LoadCatalog(b2Catalog);
        var b1Entries = ExtractEntries(b1, "B1");
        var b2Entries = ExtractEntries(b2, "B2");

        var b1Addresses = new HashSet<string>();
        var b1BundleInternalIds = new HashSet<string>();
        var b1BundleLogical = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var e in b1Entries)
        {
            if (e.Provider == providerAssetBundle)
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
            if (e.Provider != providerAssetBundle) continue;
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
        var addedB2BundleInternalIds = new HashSet<string>();
        int skippedAddress = 0;
        int skippedDuplicateBundle = 0;
        int addedB2 = 0;

        foreach (var e in b2Entries)
        {
            bool isBundleEntry = e.Provider == providerAssetBundle;
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

            if (isBundleEntry)
                addedB2BundleInternalIds.Add(e.InternalId);

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
            CopyB2Bundles(b1BundleRoot, b2BundleRoot, addedB2BundleInternalIds);
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

        const string providerAssetBundle = "UnityEngine.ResourceManagement.ResourceProviders.AssetBundleProvider";

        var main = LoadCatalog(mainCatalog);
        var source = LoadCatalog(extraSourceCatalog);
        var mainEntries = ExtractEntries(main, "Main");
        var sourceEntries = ExtractEntries(source, "ExtraSource");

        var mainAddresses = new HashSet<string>();
        var mainBundleInternalIds = new HashSet<string>();
        var mainBundleLogical = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var e in mainEntries)
        {
            if (e.Provider == providerAssetBundle)
            {
                mainBundleInternalIds.Add(NormalizeBundleId(e.InternalId));
                var logical = GetBundleLogicalName(e.InternalId);
                if (!mainBundleLogical.TryGetValue(logical, out var list))
                    mainBundleLogical[logical] = list = new List<string>();
                list.Add(GetBundleKey(e.InternalId));
            }
            else if (e.Keys.Count > 0 && e.Keys[0] is string s)
            {
                mainAddresses.Add(s);
            }
        }

        var sourceBundleLogical = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var e in sourceEntries)
        {
            if (e.Provider != providerAssetBundle) continue;
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
        var addedBundleInternalIds = new HashSet<string>();
        var addressSet = new HashSet<string>();
        int skippedAddress = 0;
        int skippedBundle = 0;

        foreach (var e in sourceEntries)
        {
            bool isBundleEntry = e.Provider == providerAssetBundle;
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
                if (e.Dependencies[i] is string dep && keyRemap.TryGetValue(dep, out var mapped))
                    e.Dependencies[i] = mapped;
            }

            mergedEntries.Add(e);
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




    static void CopyExtraBundles(string sourceBundleRoot, string outputBundleRoot, HashSet<string> addedBundleInternalIds)
    {
        var srcBase = Path.Combine(sourceBundleRoot, "StandaloneWindows64");
        var dstBase = Path.Combine(outputBundleRoot, "StandaloneWindows64");
        int copied = 0;
        int skippedSame = 0;
        int missing = 0;

        const string marker = "StandaloneWindows64\\";
        foreach (var internalId in addedBundleInternalIds)
        {
            int idx = internalId.IndexOf(marker, StringComparison.Ordinal);
            if (idx < 0) continue;
            var rel = internalId.Substring(idx + marker.Length).Replace('/', Path.DirectorySeparatorChar);
            var src = Path.Combine(srcBase, rel);
            var dst = Path.Combine(dstBase, rel);
            if (!File.Exists(src))
            {
                Debug.LogWarning($"[Extra] missing source bundle: {rel}");
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

                Debug.LogWarning($"[Extra] conflict different content: {rel}");
                continue;
            }

            File.Copy(src, dst);
            copied++;
        }

        Debug.Log($"[Extra] bundle copy done: copied={copied} skippedSame={skippedSame} missing={missing}");
    }


    static void CopyB2Bundles(string b1BundleRoot, string b2BundleRoot, HashSet<string> bundleInternalIds)
    {
        var srcBase = Path.Combine(b2BundleRoot, "StandaloneWindows64");
        var dstBase = Path.Combine(b1BundleRoot, "StandaloneWindows64");
        int copied = 0;
        int skippedSame = 0;
        int conflictDifferent = 0;
        int missing = 0;

        const string marker = "StandaloneWindows64\\";
        foreach (var internalId in addedB2BundleInternalIds)
        {
            int idx = internalId.IndexOf(marker, StringComparison.Ordinal);
            if (idx < 0) continue;

            var rel = internalId.Substring(idx + marker.Length).Replace('/', Path.DirectorySeparatorChar);
            var src = Path.Combine(srcBase, rel);
            var dst = Path.Combine(dstBase, rel);

            if (!File.Exists(src))
            {
                Debug.LogWarning($"[Merge] Missing B2 source bundle: {rel}");
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

                Debug.LogWarning($"[Merge] Conflicting bundle with different content: {rel}");
                conflictDifferent++;
                continue;
            }

            File.Copy(src, dst);
            copied++;
        }

        Debug.Log($"[Merge] bundle copy done: copied={copied} skippedSame={skippedSame} conflictsDifferent={conflictDifferent} missing={missing}");
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
        var data = JsonUtility.FromJson<ContentCatalogData>(json);
        return data;
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

            // 必须把 PrimaryKey 放在 Keys 的第一位，否则 SetData 会把它当成非主 key，
            // 导致后续合并时无法正确识别重复 address。
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
            var entry = new ContentCatalogDataEntry(loc.ResourceType, internalId, loc.ProviderId, keys, deps, loc.Data);
            result.Add(entry);
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
                // Sometimes the editor path has forward slashes and the suffix has backslashes already.
                var normalizedRt = rt.Replace('\\', '/');
                var normalizedId = id.Replace('\\', '/');
                if (normalizedId.StartsWith(normalizedRt + "/", StringComparison.Ordinal))
                    return TokenRuntimePath + "\\" + normalizedId.Substring(normalizedRt.Length + 1).Replace('/', '\\');
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
        const string marker = "StandaloneWindows64\\";
        int idx = internalId.IndexOf(marker, StringComparison.Ordinal);
        return idx < 0 ? internalId : internalId.Substring(idx + marker.Length);
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
