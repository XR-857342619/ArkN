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
    public static Database Instance => instance == null ?
#if UNITY_EDITOR
        instance = new Database().Init1()
#else
            instance = new Database().Init()
#endif
            : instance;
    private static Database instance;

    Dictionary<Type, IConfig[]> dic = new Dictionary<Type, IConfig[]>();
    public List<int> globalSkills = new List<int>();

    public void Clear()
    {
        dic.Clear();
    }

    public async Task Init()
    {
        Debug.Log("init database");
        try
        {
            if (dic.Count > 0) return;
            await Task.WhenAll(
            //AddAsync<MapTileData>("MapTileData"),
            AddAsync<CardData>("CardData"),
            AddAsync<UnitData>("UnitData"),
            //AddAsync<MapData>("MapData"),
            AddAsync<SkillData>("SkillData"),
            AddAsync<BuffData>("BuffData"),
            AddAsync<BulletData>("BulletData"),
            //AddAsync<WaveData>("WaveData"),
            AddAsync<ModifyData>("ModifyData"),
            AddAsync<EffectData>("EffectData"),
            //AddAsync<RelicData>("RelicData"),
            AddAsync<ContractData>("ContractData")
            //AddAsync<EventData>("EventData"),
            //AddAsync<RewardData>("RewardData"),
            //AddAsync<DungeonLevelData>("DungeonLevelData"),
            //AddAsync<SystemData>("SystemData")
            );
        }
        catch (Exception e)
        {
            Debug.LogError(e);
        }
    }

    public Database Init1()
    {
        Debug.Log("init1 database");
        try
        {
            if (dic.Count > 0) return this;
            //Add<MapTileData>("MapTileData");
            Add<CardData>("CardData");
            Add<UnitData>("UnitData");
            //Add<MapData>("MapData");
            Add<SkillData>("SkillData");
            Add<BuffData>("BuffData");
            Add<BulletData>("BulletData");
            //Add<WaveData>("WaveData");
            Add<ModifyData>("ModifyData");
            Add<EffectData>("EffectData");
            //Add<RelicData>("RelicData");
            Add<ContractData>("ContractData");
            //Add<EventData>("EventData");
            //Add<RewardData>("RewardData");
            //Add<DungeonLevelData>("DungeonLevelData");
            //Add<SystemData>("SystemData");
        }
        catch (Exception e)
        {
            Debug.LogError(e);
        }
        return this;
    }

    #region
    //public T Get<T>(int id) where T : class, IConfig
    //{
    //    dic.TryGetValue(typeof(T), out IConfig[] r);
    //    if (r == null || id < 0 || id >= r.Length)
    //    {
    //        Debug.LogWarning($"cant find {typeof(T).Name} ,id {id}");
    //        return null;
    //    }
    //    return r[id] as T;
    //}

    //public T Get<T>(string id) where T : class, IConfig
    //{
    //    dic.TryGetValue(typeof(T), out IConfig[] r);
    //    Debug.Log(id);
    //    return r.FirstOrDefault(x => x.Id == id) as T;
    //}

    //public T Get<T>(Func<T, bool> match) where T : class, IConfig
    //{
    //    dic.TryGetValue(typeof(T), out IConfig[] r);
    //    return r.FirstOrDefault(x => match(x as T)) as T;
    //}

    //public T[] GetAll<T>() where T : class, IConfig
    //{
    //    dic.TryGetValue(typeof(T), out IConfig[] r);
    //    return r?.Select(x => x as T).ToArray();
    //}

    //public int GetIndex<T>(T t) where T : class, IConfig
    //{
    //    dic.TryGetValue(typeof(T), out IConfig[] r);
    //    var result = Array.IndexOf(r, t);
    //    if (result == -1) throw new Exception($"cant find {typeof(T).Name} ,id {t.Id}");
    //    return result;
    //}

    //public int GetIndex<T>(string id) where T : class, IConfig
    //{
    //    dic.TryGetValue(typeof(T), out IConfig[] r);
    //    var result = Array.FindIndex(r, x => x.Id == id);
    //    if (result == -1) throw new Exception($"cant find {typeof(T).Name} ,id {id}");
    //    return result;
    //}

    #endregion

    public bool TryGet<T>(int id, out T result) where T : class, IConfig
    {
        result = null;

        if (!dic.TryGetValue(typeof(T), out IConfig[] configs) || configs == null)
        {
            TipManager.Instance.SendMessage("No data loaded for type " + typeof(T).Name);
            Debug.LogWarning($"No data loaded for type {typeof(T).Name}");
            return false;
        }

        if (id < 0 || id >= configs.Length)
        {
            TipManager.Instance.SendMessage($"Invalid id {id} for type {typeof(T).Name}. Valid range: 0-{configs.Length - 1}");
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
            TipManager.Instance.SendMessage("No data loaded for type " + typeof(T).Name);
            Debug.LogWarning($"No data loaded for type {typeof(T).Name}");
            return false;
        }

        if (string.IsNullOrEmpty(id))
        {
            TipManager.Instance.SendMessage($"Invalid (null or empty) id for type {typeof(T).Name}");
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
            TipManager.Instance.SendMessage("No data loaded for type " + typeof(T).Name);
            Debug.LogWarning($"No data loaded for type {typeof(T).Name}");
            return null;
        }

        if (match == null)
        {
            TipManager.Instance.SendMessage("Null match function for type " + typeof(T).Name);
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
            TipManager.Instance.SendMessage("No data loaded for type " + typeof(T).Name);
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
            TipManager.Instance.SendMessage("Null config for type " + typeof(T).Name);
            Debug.LogWarning($"Null config for type {typeof(T).Name}");
            return false;
        }

        if (!dic.TryGetValue(typeof(T), out IConfig[] configs) || configs == null)
        {
            TipManager.Instance.SendMessage("No data loaded for type " + typeof(T).Name);
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
            TipManager.Instance.SendMessage($"Invalid (null or empty) id for type {typeof(T).Name}");
            Debug.LogWarning($"Invalid (null or empty) id for type {typeof(T).Name}");
            return false;
        }

        if (!dic.TryGetValue(typeof(T), out IConfig[] configs) || configs == null)
        {
            TipManager.Instance.SendMessage("No data loaded for type " + typeof(T).Name);
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

        throw new Exception($"Can't find {typeof(T).Name} with id {config?.Id}");
    }

    public int GetIndex<T>(string id) where T : class, IConfig
    {
        if (TryGetIndex<T>(id, out int index))
        {
            return index;
        }

        throw new Exception($"Can't find {typeof(T).Name} with id {id}");
    }

    private void Add<T>(string name) where T : IConfig
    {
//#if UNITY_EDITOR
        var text = SaveHelper.LoadFile("/Data/" + name + ".txt"); 
        if (string.IsNullOrEmpty(text))
        {
            //Debug.Log(name + "load from address");
            text = UnityEditor.AssetDatabase.LoadAssetAtPath<TextAsset>(PathHelper.DataPath + name + ".txt").text;
        }
        var arr = text.Split('\n');
        //Debug.Log(PathHelper.AppResPath + "/Data/" + name + ".txt");
        //var arr = File.ReadLines(PathHelper.AppHotfixResPath + "/Data/" + name + ".txt")
        //.Where(line => !string.IsNullOrWhiteSpace(line))
        //.ToArray();
        //foreach (var s in arr)
        //{
        //    //if (s == "" || s == "\n")
        //        Debug.Log("empty line"+s+"in"+name);
        //}
        //var arr = text.Split('\n').Where(line => !string.IsNullOrWhiteSpace(line)).ToArray();
        IConfig[] values = new IConfig[arr.Length];
        for (int i = 0; i < arr.Length; i++)
        {
            if (JsonHelper.FromJson<T>(arr[i])?.Id == null && i != arr.Length - 1)
            {
                //Debug.Log(JsonHelper.FromJson<T>(arr[i]).Id);
                continue;
            }
            try
            {
                values[i] = JsonHelper.FromJson<T>(arr[i]);
                if (typeof(T) == typeof(SkillData))
                {
                    SkillData skill = values[i] as SkillData?? null;
                    if (skill == null)
                        continue;
                    if (skill.Type == "全局技能")
                    {
                        globalSkills.Add(i);
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError(typeof(T).Name + "," + (i+1) + "\n" + e.ToString());
            }
        }
        dic.Add(typeof(T), values);
//#endif
    }

    private async Task AddAsync<T>(string name) where T : IConfig
    {
        string text;
        text = SaveHelper.LoadFile("/Data/" + name + ".txt");
        //Debug.Log(PathHelper.AppHotfixResPath + "/Data/" + name + ".txt");
        //Debug.Log(PathHelper.AppResPath + "/Data/" + name + ".txt");
        Debug.Log(name);
        if (string.IsNullOrEmpty(text))
        {
            //Debug.Log(name + "load from address");
            var operation = Addressables.LoadAssetAsync<TextAsset>(PathHelper.DataPath + name);
            var txt= operation.WaitForCompletion().text;
            await operation.Task;
            txt = operation.Result.text;
            Addressables.ReleaseInstance(operation);
        }
        if (string.IsNullOrEmpty(text)) return;
        //var arr = text.Split('\n');
        //var arr = text.Split('\n').Where(line => !string.IsNullOrWhiteSpace(line)).ToArray();
        var arr = text.Split('\n').Where(line => !string.IsNullOrWhiteSpace(line)).ToArray();
        //var arr = File.ReadLines(PathHelper.AppResPath + "/Data/" + name + ".txt")
        //      .Where(line => !string.IsNullOrWhiteSpace(line))
        //      .ToArray();
        IConfig[] values = new IConfig[arr.Length];
        for (int i = 0; i < arr.Length; i++)
        {
            try
            {
                //if (values[i] == null)
                //{
                //    Debug.LogError($"{name}\n {arr[i]}");
                //    continue;
                //}
                values[i] = JsonHelper.FromJson<T>(arr[i]);
                if (typeof(T) == typeof(SkillData))
                {
                    SkillData skill = values[i] as SkillData ?? null;
                    if (skill == null)
                        continue;
                    if (skill.Type == "全局技能")
                    {
                        globalSkills.Add(i);
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError(arr[i] + "\n" + e.ToString());
            }
        }
        //Debug.Log(Time.time);
        dic.Add(typeof(T), values);
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
        //获取全部Excel文件夹的路径
        var path = PathHelper.ExcelResPath + "\\Excel\\";
        List<string> paths = Directory.GetDirectories(path).ToList();
        paths.RemoveAll(x => x.Contains("$"));
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

        //FileHelper.GetAllFiles(paths, path);
        return paths;

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
