using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
    readonly Dictionary<Type, Dictionary<string, IConfig>> byId = new Dictionary<Type, Dictionary<string, IConfig>>();
    readonly Dictionary<Type, Dictionary<string, int>> indexById = new Dictionary<Type, Dictionary<string, int>>();
    public List<int> globalSkills = new List<int>();
    public void Clear()
    {
        dic.Clear();
        byId.Clear();
        indexById.Clear();
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
            else
            {
                BuildLookupAndBind();
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
            BuildLookupAndBind();
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

        if (byId.TryGetValue(typeof(T), out var map) && map.TryGetValue(id, out var hit))
        {
            result = hit as T;
            return result != null;
        }

        // 兼容：lookup 尚未建立时退回线性扫描
        result = configs.FirstOrDefault(x => x?.Id == id) as T;
        return result != null;
    }

    /// <summary>把数组下标转换为稳定字符串 Id；越界或空返回 null。</summary>
    public string GetId<T>(int index) where T : class, IConfig
    {
        if (!dic.TryGetValue(typeof(T), out IConfig[] configs) || configs == null) return null;
        if (index < 0 || index >= configs.Length) return null;
        return configs[index]?.Id;
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

        // 不依赖文件系统枚举顺序：重复 Id 的“第一条”必须可复现
        result.Sort(StringComparer.OrdinalIgnoreCase);
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

    /// <summary>
    /// 建立 Type -> Id -> config / index 的运行时索引，并把新格式的 *Id 字段
    /// 回填到旧 int 索引字段，保证旧代码兼容。
    /// </summary>
    private void BuildLookupAndBind()
    {
        byId.Clear();
        indexById.Clear();

        foreach (var kv in dic)
        {
            var arr = kv.Value;
            if (arr == null) continue;

            var idMap = new Dictionary<string, IConfig>(StringComparer.Ordinal);
            var idxMap = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < arr.Length; i++)
            {
                var cfg = arr[i];
                if (cfg == null || string.IsNullOrEmpty(cfg.Id)) continue;
                // 重复 Id 在 BuildConfigArray 已报告；这里保留第一份，避免 lookup 抖动
                if (!idMap.ContainsKey(cfg.Id))
                {
                    idMap[cfg.Id] = cfg;
                    idxMap[cfg.Id] = i;
                }
            }
            byId[kv.Key] = idMap;
            indexById[kv.Key] = idxMap;
        }

        BindStringReferences();
    }

    /// <summary>
    /// 把带 [ConfigRef] 的 string/string[] 解析回 int/int[] 旧字段。
    /// 仅当新格式数据包含 *Id 字段时生效；老格式没有该字段时保持旧行为。
    /// </summary>
    private void BindStringReferences()
    {
        foreach (var kv in dic)
        {
            var arr = kv.Value;
            if (arr == null) continue;
            for (int i = 0; i < arr.Length; i++)
            {
                var cfg = arr[i];
                if (cfg != null) BindOne(cfg);
            }
        }
    }

    private void BindOne(IConfig cfg)
    {
        var type = cfg.GetType();
        var fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public);
        for (int i = 0; i < fields.Length; i++)
        {
            var field = fields[i];
            var attr = field.GetCustomAttribute<ConfigRefAttribute>();
            if (attr == null) continue;

            bool isArray = field.FieldType == typeof(string[]);
            bool isScalar = field.FieldType == typeof(string);
            if (!isArray && !isScalar) continue;

            string suffix = isArray ? "Ids" : "Id";
            if (!field.Name.EndsWith(suffix, StringComparison.Ordinal)) continue;
            string legacyName = field.Name.Substring(0, field.Name.Length - suffix.Length);
            var legacy = type.GetField(legacyName, BindingFlags.Instance | BindingFlags.Public);
            if (legacy == null)
            {
                AddInitError($"[ConfigRef] {type.Name}.{field.Name} 找不到对应旧字段 {legacyName}");
                continue;
            }

            if (isScalar)
            {
                string id = field.GetValue(cfg) as string;
                if (string.IsNullOrEmpty(id)) continue;
                int index = ResolveIndex(attr.Table, id);
                if (index < 0)
                {
                    AddInitError($"[ConfigRef] Sheet \"{type.Name}\" → Id \"{cfg.Id}\" → 字段 \"{field.Name}\"：无效 ID \"{id}\"（目标表 {attr.Table} 中不存在）");
                    continue;
                }
                SetLegacyInt(legacy, cfg, index);
            }
            else
            {
                var ids = field.GetValue(cfg) as string[];
                if (ids == null || ids.Length == 0) continue;
                var indexes = new int[ids.Length];
                bool ok = true;
                for (int k = 0; k < ids.Length; k++)
                {
                    indexes[k] = ResolveIndex(attr.Table, ids[k]);
                    if (indexes[k] < 0)
                    {
                        AddInitError($"[ConfigRef] Sheet \"{type.Name}\" → Id \"{cfg.Id}\" → 字段 \"{field.Name}\"[{k}]：无效 ID \"{ids[k]}\"（目标表 {attr.Table} 中不存在）");
                        ok = false;
                    }
                }
                if (ok) legacy.SetValue(cfg, indexes);
            }
        }
    }

    private int ResolveIndex(string table, string id)
    {
        var t = typeof(IConfig).Assembly.GetType(table);
        if (t == null || !indexById.TryGetValue(t, out var map)) return -1;
        return map.TryGetValue(id, out var index) ? index : -1;
    }

    private static void SetLegacyInt(FieldInfo legacy, object cfg, int index)
    {
        if (legacy.FieldType == typeof(int)) legacy.SetValue(cfg, index);
        else if (legacy.FieldType == typeof(int?)) legacy.SetValue(cfg, (int?)index);
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
                {
                    if (dict.ContainsKey(cfg.Id))
                    {
                        // 重复 Id 是数据错误；运行时采用“第一条定义获胜”的确定性策略，
                        // 避免结果随 Directory.GetFiles 枚举顺序变化。
                        AddInitError($"[Config] Sheet \"{name}\" → Id \"{cfg.Id}\"：重复 ID（重复定义已忽略，保留第一条）");
                    }
                    else
                    {
                        dict[cfg.Id] = cfg;
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"{typeof(T).Name} 解析失败: {line}\n{e}");
            }
        }

        if (index != null && index.Count > 0)
        {
            var values = new IConfig[index.Count];
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < index.Count; i++)
            {
                if (!seen.Add(index[i]))
                    AddInitError($"[Config] Sheet \"{name}\" → _index.txt 第 {i + 1} 行 → Id \"{index[i]}\"：重复索引条目");
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
        string key = packageName + ":" + mapName;
        if (Maps.TryGetValue(key, out MapInfo result) && result != null)
        {
            return result;
        }

        // ===== 读取优先级：玩家存档 > 出厂内置版本 =====
        // v3.5 只修了**编辑器**这一侧（.txt 为先），打包分支仍然是"先读 Addressables、最后才读 .map"，
        // 于是打包后凡是"随包烘进去"的地图，玩家在游戏里改完（比如刷皮肤）保存、重启游戏后
        // 又会被内置的旧版本盖回去——表现为"局内能看到皮肤、重启就丢"。
        // 现在两条路径统一为：本机落盘的存档文件永远最先读，内置版本只作兜底。
        string str = string.Empty;
        string strSource = null;

#if UNITY_EDITOR
        // 1) 编辑器：Assets 下的 .txt 是 m_save 的直接落盘物（Save 的编辑器分支），最高优先级
        {
            var editorPath = System.IO.Path.Combine(
                Directory.GetCurrentDirectory(),
                "Assets/Bundles/Data/Map/" + packageName + "/" + mapName + ".txt");
            if (File.Exists(editorPath))
            {
                try { str = File.ReadAllText(editorPath); strSource = "编辑器 .txt"; }
                catch (Exception e) { Debug.LogError($"[Database] 读取地图存档失败 {editorPath}: {e.Message}"); }
            }
        }
#endif
        // 2) 玩家存档：热更/持久化目录下的 .map（打包环境为最高优先级；编辑器下正常不存在）
        if (string.IsNullOrEmpty(str))
        {
            str = SaveHelper.LoadMap("/Map/" + packageName + "/" + mapName + ".map");
            if (!string.IsNullOrEmpty(str)) strSource = "玩家存档 .map";
        }

        // 3) 内置版本：打包时烘进 Addressables 的出厂地图，只作兜底
        if (string.IsNullOrEmpty(str))
        {
            str = SaveHelper.LoadBaseFile(packageName, mapName);
            if (!string.IsNullOrEmpty(str)) strSource = "内置 Addressables";
        }

        // 加载/解析失败：不把 null 缓存进 Maps（否则该地图在本会话内永远无法恢复），
        // 直接报错返回，待存档文件就位后可重新加载
        if (string.IsNullOrEmpty(str))
        {
            Debug.LogError($"[Database] 地图加载失败：{packageName}/{mapName}（Addressables 未注册该地图，且对应存档文件不存在）");
            Maps.Remove(key);
            return null;
        }

        Debug.Log($"[Database] 读取地图 {key}  来源：{strSource ?? "?"}  长度 {str.Length}");

        // R7：解析过程要兜住异常。以前 $type 解析失败/文件被写坏等情况会直接抛 JsonSerializationException，
        // 一路炸到调用方（表现为"点加载没反应"），这里统一转成一条明确错误 + 返回 null。
        try
        {
            result = JsonHelper.FromJson<MapInfo>(str);
        }
        catch (Exception e)
        {
            Debug.LogError($"[Database] 地图解析异常：{packageName}/{mapName}\n" +
                           $"{e.GetType().Name}: {e.Message}");
            result = null;
        }
        if (result == null)
        {
            Debug.LogError($"[Database] 地图解析失败：{packageName}/{mapName}");
            Maps.Remove(key);
            return null;
        }

        Maps[key] = result;
        return result;
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
        // 同一个目录里可能同时存在 **出厂 .txt** 与 **玩家存档 .map**（打包环境下两者同处热更目录），
        // 直接取文件名会出现「同名两条」，这里按名字去重（顺序保持首次出现）
        var seen = new HashSet<string>();
        var names = new List<string>();
        foreach (var f in files)
        {
            var name = System.IO.Path.GetFileNameWithoutExtension(f);
            if (seen.Add(name)) names.Add(name);
        }
        return names;
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
