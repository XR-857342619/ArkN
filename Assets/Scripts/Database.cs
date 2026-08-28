using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using System.IO;
using UnityEngine.AddressableAssets;

public class Database
{
    public static Database Instance => instance == null ? instance = new Database() : instance;
    private static Database instance;

    Dictionary<Type, IConfig[]> dic = new Dictionary<Type, IConfig[]>();
    public List<int> globalSkills = new List<int>();
    public void Clear()
    {
        dic.Clear();
    }

    /// <summary>
    /// 编辑器/非运行态下不创建 TipManager（避免 DontDestroyOnLoad 只能在 Play Mode 使用的问题），改用日志提示。
    /// </summary>
    static void ShowTip(string message)
    {
        if (Application.isPlaying)
            TipManager.Instance.ShowTip(message);
        else
            Debug.LogWarning(message);
    }

    static void AddInitError(string message)
    {
        if (Application.isPlaying)
            TipManager.Instance.initErorrTips.Add(message);
        else
            Debug.LogWarning(message);
    }


    public async Task Init() => await Init(null);

    public async Task Init(List<string> excludeExcelPaths)
    {
        try
        {
            Debug.Log("Database init");
            if (dic.Count > 0) return;

            var excludeNames = excludeExcelPaths?
                .Where(f => !string.IsNullOrEmpty(f))
                .Select(Path.GetFileNameWithoutExtension)
                .Where(n => !string.IsNullOrEmpty(n))
                .ToList() ?? new List<string>();

            var tasks = new List<Task>
            {
                AddAsync<CardData>("CardData", excludeNames),
                AddAsync<UnitData>("UnitData", excludeNames),
                AddAsync<SkillData>("SkillData", excludeNames),
                AddAsync<SkillJsonData>("SkillJson", excludeNames),
                AddAsync<BuffData>("BuffData", excludeNames),
                AddAsync<BulletData>("BulletData", excludeNames),
                AddAsync<ModifyData>("ModifyData", excludeNames),
                AddAsync<EffectData>("EffectData", excludeNames),
                AddAsync<ContractData>("ContractData", excludeNames),
                AddAsync<SpineData>("SpineData", excludeNames)
            };

            await Task.WhenAll(tasks);

            // 如果分类数据目录没有加载到有效数据，回退到内部统一大文件
            if (NeedFallbackToInternalData())
            {
                Debug.LogWarning("分类数据目录未加载到有效数据，回退到内部数据 Init1");
                Clear();
                Init1();
            }
        }
        catch (Exception e)
        {
            ShowTip("Init database failed: " + e.Message);
            Debug.LogError(e);
        }
    }

    public Database Init1() => Init1(null);

    public Database Init1(List<string> excludeExcelPaths)
    {
        try
        {
            Debug.Log("Database init1");
            if (dic.Count > 0) return this;

            var excludeNames = excludeExcelPaths?
                .Where(f => !string.IsNullOrEmpty(f))
                .Select(Path.GetFileNameWithoutExtension)
                .Where(n => !string.IsNullOrEmpty(n))
                .ToList() ?? new List<string>();

            Add<CardData>("CardData", excludeNames);
            Add<UnitData>("UnitData", excludeNames);
            Add<SkillData>("SkillData", excludeNames);
            Add<SkillJsonData>("SkillJson", excludeNames);
            Add<BuffData>("BuffData", excludeNames);
            Add<BulletData>("BulletData", excludeNames);
            Add<ModifyData>("ModifyData", excludeNames);
            Add<EffectData>("EffectData", excludeNames);
            Add<ContractData>("ContractData", excludeNames);
            Add<SpineData>("SpineData", excludeNames);
        }
        catch (Exception e)
        {
            AddInitError(e.Message);
            Debug.LogError(e);
        }
        return this;
    }

    public bool TryGet<T>(int id, out T result) where T : class, IConfig
    {
        result = null;

        if (!dic.TryGetValue(typeof(T), out IConfig[] configs) || configs == null)
        {
            ShowTip("No data loaded for type " + typeof(T).Name);
            Debug.LogWarning($"No data loaded for type {typeof(T).Name}");
            return false;
        }

        if (id < 0 || id >= configs.Length)
        {
            ShowTip($"Invalid id {id} for type {typeof(T).Name}. Valid range: 0-{configs.Length - 1}");
            Debug.LogWarning($"Invalid id {id} for type {typeof(T).Name}. Valid range: 0-{configs.Length - 1}");
            return false;
        }

        result = configs[id] as T;
        return result != null;
    }

    public bool TryGet<T>(string id, out T result) where T : class, IConfig
    {
        result = null;

        if (!dic.TryGetValue(typeof(T), out IConfig[] configs) || configs == null)
        {
            ShowTip("No data loaded for type " + typeof(T).Name);
            Debug.LogWarning($"No data loaded for type {typeof(T).Name}");
            return false;
        }

        if (string.IsNullOrEmpty(id))
        {
            ShowTip($"Invalid (null or empty) id for type {typeof(T).Name}");
            Debug.LogWarning($"Invalid (null or empty) id for type {typeof(T).Name}");
            return false;
        }

        result = configs.FirstOrDefault(x => x?.Id == id) as T;
        return result != null;
    }

    public T Get<T>(int id) where T : class, IConfig
    {
        if (TryGet(id, out T result))
        {
            return result;
        }

        return null;
    }

    public T Get<T>(string id) where T : class, IConfig
    {
        if (TryGet(id, out T result))
        {
            return result;
        }

        return null;
    }

    public T Get<T>(Func<T, bool> match) where T : class, IConfig
    {
        if (!dic.TryGetValue(typeof(T), out IConfig[] configs) || configs == null)
        {
            ShowTip("No data loaded for type " + typeof(T).Name);
            Debug.LogWarning($"No data loaded for type {typeof(T).Name}");
            return null;
        }

        if (match == null)
        {
            ShowTip("Null match function for type " + typeof(T).Name);
            Debug.LogWarning($"Null match function for type {typeof(T).Name}");
            return null;
        }

        return configs
            .Where(x => x != null)
            .Select(x => x as T)
            .FirstOrDefault(match);
    }

    public T[] GetAll<T>() where T : class, IConfig
    {
        if (!dic.TryGetValue(typeof(T), out IConfig[] configs) || configs == null)
        {
            ShowTip("No data loaded for type " + typeof(T).Name);
            Debug.LogWarning($"No data loaded for type {typeof(T).Name}");
            return Array.Empty<T>();
        }

        return configs
            .Where(x => x != null)
            .Select(x => x as T)
            .ToArray();
    }

    public bool TryGetIndex<T>(T config, out int index) where T : class, IConfig
    {
        index = -1;

        if (config == null)
        {
            ShowTip("Null config for type " + typeof(T).Name);
            Debug.LogWarning($"Null config for type {typeof(T).Name}");
            return false;
        }

        if (!dic.TryGetValue(typeof(T), out IConfig[] configs) || configs == null)
        {
            ShowTip("No data loaded for type " + typeof(T).Name);
            Debug.LogWarning($"No data loaded for type {typeof(T).Name}");
            return false;
        }

        index = Array.IndexOf(configs, config);
        return index >= 0;
    }

    public bool TryGetIndex<T>(string id, out int index) where T : class, IConfig
    {
        index = -1;

        if (string.IsNullOrEmpty(id))
        {
            ShowTip($"Invalid (null or empty) id for type {typeof(T).Name}");
            Debug.LogWarning($"Invalid (null or empty) id for type {typeof(T).Name}");
            return false;
        }

        if (!dic.TryGetValue(typeof(T), out IConfig[] configs) || configs == null)
        {
            ShowTip("No data loaded for type " + typeof(T).Name);
            Debug.LogWarning($"No data loaded for type {typeof(T).Name}");
            return false;
        }

        for (int i = 0; i < configs.Length; i++)
        {
            if (configs[i]?.Id == id)
            {
                index = i;
                return true;
            }
        }

        return false;
    }

    public int GetIndex<T>(T config) where T : class, IConfig
    {
        if (TryGetIndex(config, out int index))
        {
            return index;
        }
        ShowTip($"Can't find {typeof(T).Name} with id {config?.Id}");
        //throw new Exception($"Can't find {typeof(T).Name} with id {config?.Id}");
        return -1;
    }

    public int GetIndex<T>(string id) where T : class, IConfig
    {
        if (TryGetIndex<T>(id, out int index))
        {
            return index;
        }
        ShowTip($"Can't find {typeof(T).Name} with id {id}");
        //throw new Exception($"Can't find {typeof(T).Name} with id {id}");
        return -1;
    }

    private void Add<T>(string name, List<string> excludeFileNames = null) where T : IConfig
    {
#if UNITY_EDITOR
        var files = ResolveDataFiles(name, excludeFileNames);
        var lines = new List<string>();

        // 分类文件夹为空时回退到旧版统一大文件
        if (files.Count == 0)
        {
            string text = SaveHelper.LoadFile("/Data/" + name + ".txt");
            if (string.IsNullOrEmpty(text))
            {
                var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<TextAsset>(PathHelper.DataPath + name + ".txt");
                text = asset != null ? asset.text : null;
            }

            if (!string.IsNullOrEmpty(text))
                lines.AddRange(text.Split('\n').Where(l => !string.IsNullOrWhiteSpace(l)));
        }
        else
        {
            foreach (var file in files)
            {
                try
                {
                    lines.AddRange(File.ReadAllLines(file).Where(l => !string.IsNullOrWhiteSpace(l)));
                }
                catch (Exception e)
                {
                    Debug.LogError($"读取数据文件失败: {file}\n{e}");
                }
            }
        }

        var values = BuildConfigArray<T>(name, lines, out var globalSkillIndexes);
        foreach (int index in globalSkillIndexes)
        {
            if (!globalSkills.Contains(index))
                globalSkills.Add(index);
        }

        dic[typeof(T)] = values;
#endif
    }

    private async Task AddAsync<T>(string name, List<string> excludeFileNames = null) where T : IConfig
    {
        var files = ResolveDataFiles(name, excludeFileNames);
        // 兼容旧版单文件 Data/XXX.txt
        if (files.Count == 0)
        {
            string text = SaveHelper.LoadFile("/Data/" + name + ".txt");
            //Debug.Log($"load: /Data/{name}");
            if (string.IsNullOrEmpty(text))
            {
                var operation = Addressables.LoadAssetAsync<TextAsset>(PathHelper.DataPath + name);
                await operation.Task;
                text = operation.Result?.text;
                if (operation.IsValid()) Addressables.Release(operation);
            }
            if (string.IsNullOrEmpty(text)) return;
            var arr = text.Split('\n').Where(line => !string.IsNullOrWhiteSpace(line)).ToArray();
            var oldValues = new IConfig[arr.Length];
            for (int i = 0; i < arr.Length; i++)
            {
                try
                {
                    oldValues[i] = JsonHelper.FromJson<T>(arr[i]);
                    if (typeof(T) == typeof(SkillData))
                    {
                        SkillData skill = oldValues[i] as SkillData;
                        if (skill != null && skill.Type == "全局技能")
                            globalSkills.Add(i);
                    }
                }
                catch (Exception e)
                {
                    AddInitError(arr[i] + "\n" + e.ToString());
                    Debug.LogError(arr[i] + "\n" + e.ToString());
                }
            }
            dic[typeof(T)] = oldValues;
            return;
        }
        //Debug.Log($"files: {files.Count}");
        var lines = new List<string>();
        foreach (var file in files)
        {
            try
            {
                lines.AddRange(File.ReadAllLines(file).Where(l => !string.IsNullOrWhiteSpace(l)));
                //Debug.Log(string.Join(", ", lines));
            }
            catch (Exception e)
            {
                Debug.LogError($"读取数据文件失败: {file}\n{e}");
            }
        }

        var values = BuildConfigArray<T>(name, lines, out var globalSkillIndexes);
        foreach (int index in globalSkillIndexes)
        {
            if (!globalSkills.Contains(index))
                globalSkills.Add(index);
        }
        //Debug.Log(values);
        dic[typeof(T)] = values;
    }

    private static string GetDataRoot()
    {
//#if UNITY_EDITOR
//        return Path.GetFullPath(PathHelper.DataPath);
//#else
        return PathHelper.AppHotfixResPath + "/Data/";
//#endif
    }

    private static List<string> ResolveDataFiles(string name, List<string> excludeFileNames)
    {
        var result = new List<string>();
        string root = GetDataRoot();
        string folder = root + name + "/";
        //Debug.Log(folder);
        if (excludeFileNames == null)
            excludeFileNames = new List<string>();

        var excludeSet = new HashSet<string>(excludeFileNames, StringComparer.OrdinalIgnoreCase);
        //Debug.Log("除外" + string.Join(", ", excludeSet));
        //Debug.Log($"文件夹存在:{Directory.Exists(folder)}");
        if (Directory.Exists(folder))
        {
            foreach (var file in Directory.GetFiles(folder, "*.txt"))
            {
                string fileName = Path.GetFileNameWithoutExtension(file);
                if (fileName == "_index") continue;
                if (excludeSet.Contains(fileName)) continue;
                //Debug.Log(fileName);
                result.Add(file);
            }
        }

        return result;
    }

    private bool NeedFallbackToInternalData()
    {
        if (dic.Count == 0) return true;

        foreach (var arr in dic.Values)
        {
            if (arr == null || arr.Length == 0)
                return true;
        }

        return false;
    }

    private IConfig[] BuildConfigArray<T>(string name, List<string> lines, out List<int> globalSkillIndexes) where T : IConfig
    {
        globalSkillIndexes = new List<int>();

        string root = GetDataRoot();
        string indexFile = root + name + "/_index.txt";
        List<string> index = null;
        if (File.Exists(indexFile))
        {
            index = File.ReadAllLines(indexFile)
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .ToList();
        }

        var dict = new Dictionary<string, T>();
        foreach (var line in lines)
        {
            try
            {
                var cfg = JsonHelper.FromJson<T>(line);
                if (cfg != null && cfg.Id != null)
                    dict[cfg.Id] = cfg;
            }
            catch (Exception e)
            {
                Debug.LogError($"{typeof(T).Name} 解析失败: {line}\n{e}");
            }
        }

        if (index != null && index.Count > 0)
        {
            var values = new IConfig[index.Count];
            for (int i = 0; i < index.Count; i++)
            {
                if (dict.TryGetValue(index[i], out var cfg))
                {
                    values[i] = cfg;
                    if (typeof(T) == typeof(SkillData) && cfg is SkillData skill && skill.Type == "全局技能")
                        globalSkillIndexes.Add(i);
                }
            }
            return values;
        }

        // 无索引文件时退化为按行顺序
        var arr = new IConfig[lines.Count];
        for (int i = 0; i < lines.Count; i++)
        {
            try
            {
                arr[i] = JsonHelper.FromJson<T>(lines[i]);
                if (typeof(T) == typeof(SkillData) && arr[i] is SkillData skill && skill.Type == "全局技能")
                    globalSkillIndexes.Add(i);
            }
            catch (Exception e)
            {
                Debug.LogError($"{typeof(T).Name} 解析失败: {lines[i]}\n{e}");
            }
        }
        return arr;
    }
    public Dictionary<string, MapInfo> Maps = new Dictionary<string, MapInfo>();
    public MapInfo GetMap(string packageName, string mapName)
    {
        if (Maps.TryGetValue(packageName + ":" + mapName, out MapInfo result))
        {
            return result;
        }
        else
        {
            var str = SaveHelper.LoadBaseFile(mapName);
            if (string.IsNullOrEmpty(str))
                str = SaveHelper.LoadMap("/Map/" + packageName + "/" + mapName + ".map");
            result = JsonHelper.FromJson<MapInfo>(str);
            Maps.Add(packageName+ ":" +mapName, result);
            //Debug.Log(str);
            return result;
        }
    }

    public List<string> GetMapPackages()
    {
        var path = PathHelper.MapResPath + "/Map/";
        List<string> directories = new List<string>();

        if (!Directory.Exists(path)) return new List<string>();
        
        // 获取指定路径下的所有文件夹
        string[] directoriesArray = Directory.GetDirectories(path);
        directories = Directory.GetDirectories(path).Select(Path.GetFileName).ToList();
        return directories;
    }
    public List<string> GetMaps(string packageName)
    {
        var path = PathHelper.MapResPath + "/Map/" + packageName + "/";
        if (!Directory.Exists(path)) return new List<string>();
        List<string> files = new List<string>();
        FileHelper.GetAllFiles(files, path);
        files.RemoveAll(x => x.EndsWith(".meta"));
        files.RemoveAll(x => x.EndsWith(".ini"));
        return files.Select(x => System.IO.Path.GetFileNameWithoutExtension(x)).ToList();
    }
    public string GetConfigPath(string packageName)
    {
        string file = PathHelper.MapResPath + "/Map/" + packageName + "/config.ini";
        if (File.Exists(file)) return file;
        return null;
    }
    public List<string> GetExcelPathList()
    {
        //获取全部Excel文件夹的路径（使用 Path.Combine 保证跨平台路径分隔符正确）
        var path = Path.Combine(PathHelper.ExcelResPath, "Excel");
        if (!Directory.Exists(path)) return new List<string>();
        List<string> paths = Directory.GetDirectories(path)
            .Select(PathHelper.NormalizeAppPath)
            .ToList();
        return paths;
    }
    public List<string> GetExcelPathNames(List<string> paths)
    {
        //获取全部Excel文件夹的名字
        List<string> names = new List<string>();
        foreach (var file in paths)
        {
            //if (file.EndsWith(".ini")) continue;
            var name = System.IO.Path.GetFileNameWithoutExtension(file);
            names.Add(name);
        }
        return names;
    }
    public List<string> GetExcelFileList(string path)
    {
        //获取Excel文件夹下所有文件路径
        List<string> paths = new List<string>();
        paths = Directory.GetFiles(path).ToList();
        paths.RemoveAll(x => System.IO.Path.GetFileName(x).StartsWith("~$"));
        //foreach (var file in paths)
        //{
        //    Debug.Log(file);
        //}
        //FileHelper.GetAllFiles(paths, path);
        return paths.Select(PathHelper.NormalizeAppPath).ToList();

    }
    public List<string> GetExcelFileNames(List<string> paths)
    {
        //获取Excel文件夹下所有文件名字
        List<string> names = new List<string>();
        foreach (var file in paths)
        {
            //if (file.EndsWith(".ini")) continue;
            var name = System.IO.Path.GetFileNameWithoutExtension(file);
            names.Add(name);
        }
        return names;
    }
    //public List<SkillData> GetGlobalSkills()
    //{

    //}
}
