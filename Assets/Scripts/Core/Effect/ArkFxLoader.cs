// ============================================================
// ArkFxLoader - 明日方舟特效运行时加载器（v2：支持子目录归档）
// 目录约定（由 arkfx_merge.py 生成）：
//   StreamingAssets/arkfx/_deps/<hash>_lz4.ab ...   共享依赖
//   StreamingAssets/arkfx/<系列>/<名>_lz4.ab ...    各系列主包
// 加载顺序：依赖先、主包后（两遍扫描，确定性）
// ============================================================
using System.Collections.Generic;
using System.Linq;
using System.IO;
using UnityEngine;

public static class ArkFxLoader
{
    static readonly List<AssetBundle> _bundles = new List<AssetBundle>();
    static readonly Dictionary<string, AssetBundle> _byPath = new Dictionary<string, AssetBundle>();
    static readonly Dictionary<string, AssetBundle> _byName = new Dictionary<string, AssetBundle>(); // key = 文件名(小写)
    static bool _loaded;
    static string _loadDir;

    public static string AbDir =>
        Path.Combine(Application.streamingAssetsPath, "arkfx");

    /// <summary>游戏启动时调用一次（ArkFxBootstrap 自动调）</summary>
    public static void LoadAll(string abDir)
    {
        if (_loaded) return;
        if (!Directory.Exists(abDir))
        {
            Debug.LogWarning("[ArkFxLoader] 特效目录不存在: " + abDir);
            return;
        }

        _loadDir = abDir;
        var all = Directory.GetFiles(abDir, "*.ab", SearchOption.AllDirectories);

        // 第一遍：共享依赖（_deps 目录 / 哈希命名 / shader / conv 包）
        foreach (var f in all)
        {
            var n = Path.GetFileName(f).ToLowerInvariant();
            var rel = f.Replace(abDir, "").ToLowerInvariant();
            bool isDep = rel.Contains("_deps")
                      || n.Contains("shader") || n.EndsWith("_conv.ab")
                      || System.Text.RegularExpressions.Regex.IsMatch(n, @"^[0-9a-f]{8}_lz4\.ab$");
            if (isDep) Register(f);
        }

        // 第二遍：各系列主包
        foreach (var f in all)
        {
            var n = Path.GetFileName(f).ToLowerInvariant();
            var rel = f.Replace(abDir, "").ToLowerInvariant();
            bool isDep = rel.Contains("_deps")
                      || n.Contains("shader") || n.EndsWith("_conv.ab")
                      || System.Text.RegularExpressions.Regex.IsMatch(n, @"^[0-9a-f]{8}_lz4\.ab$");
            if (!isDep) Register(f);
        }

        _loaded = true;
        LoadParams();   // 提前加载：让启动日志就能确认 fxparams.json 是否可用
        Debug.Log($"[ArkFxLoader] 已加载 {_bundles.Count} 个包 | abDir={abDir} | 目录存在={Directory.Exists(abDir)}"
                  + $" | .ab 总数={all.Length} | fxparams={(_params == null ? "未加载" : _params.Count + " 个 prefab")}");
    }

    static void Register(string f)
    {
        var key = Path.GetFullPath(f).ToLowerInvariant();
        if (_byPath.ContainsKey(key)) return;
        var b = AssetBundle.LoadFromFile(f);
        if (b == null)
        {
            var expect = Path.GetFileNameWithoutExtension(f);
            foreach (var lb in AssetBundle.GetAllLoadedAssetBundles())
                if (lb != null && lb.name == expect) { b = lb; break; }
        }
        if (b != null)
        {
            _bundles.Add(b); _byPath[key] = b;
            _byName[Path.GetFileName(f).ToLowerInvariant()] = b;
        }
        else Debug.LogError("[ArkFxLoader] 包加载失败: " + f);
    }

    /// <summary>实例化特效。abPath = 主包路径（或仅文件名，自动在 abDir 下找）</summary>
    public static GameObject SpawnEffect(string abPath, string prefabName,
                                         Vector3 pos, Quaternion rot)
    {
        prefabName = (prefabName ?? "").Trim();
        var file = Path.GetFileName(abPath).ToLowerInvariant();

        // 查找顺序：文件名索引 -> 全目录定位文件后加载（与子目录层级解耦）
        AssetBundle ab = null;
        if (_byName.TryGetValue(file, out var hit) && hit != null) ab = hit;
        if (ab == null)
        {
            // 不用精确名模式（部分系统下匹配行为不可靠），通配列出后按名过滤
            var all = Directory.GetFiles(AbDir, "*.ab", SearchOption.AllDirectories);
            var match = all.FirstOrDefault(f =>
                string.Equals(Path.GetFileName(f), file, System.StringComparison.OrdinalIgnoreCase));
            if (match != null)
            {
                Register(match);
                _byName.TryGetValue(file, out ab);
            }
        }
        if (ab == null)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"[ArkFx诊断] 主包加载失败 | 要找的文件={file}");
            sb.AppendLine($"[ArkFx诊断] AbDir={AbDir} | .ab 总数={Directory.GetFiles(AbDir, "*.ab", SearchOption.AllDirectories).Length}");
            sb.AppendLine($"[ArkFx诊断] 已登记包数={_byName.Count}，键样本: {string.Join(", ", System.Linq.Enumerable.Take(_byName.Keys, 5))}");
            Debug.LogError(sb.ToString());
            return null;
        }

        var prefab = TryLoadPrefab(ab, prefabName);
        if (prefab == null)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"[ArkFx诊断] 在包 '{ab.name}' 里找不到 '{prefabName}'");
            int shown = 0;
            foreach (var n in ab.GetAllAssetNames())
            {
                if (n.IndexOf(prefabName, System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    sb.AppendLine("[ArkFx诊断]   相近资产: " + n);
                    if (++shown >= 5) break;
                }
            }
            if (shown == 0) sb.AppendLine("[ArkFx诊断]   包内无相近资产（检查 Ab File 与 Prefab Name 是否配套）");
            Debug.LogError(sb.ToString());
            return null;
        }

        var fx = Object.Instantiate(prefab, pos, rot);
        Kick(fx, prefabName);
        // 切到 FxOverlay 层：AB 实例化出的特效对象层来自 AB prefab（Default），
        // 不继承父节点，必须在出口统一切层，否则会被 Spine 无视深度盖住（层未创建时内部自动跳过）
        SpineOverlayCameraUtil.ApplyFx(fx);
        return fx;
    }

    // ================= 原版脚本参数（fxparams.json，由 dump_fx_params.py 生成） =================

    [System.Serializable]
    class FxParamFile { public int version; public FxParamPrefab[] prefabs; }

    [System.Serializable]
    class FxParamPrefab { public string prefab; public FxParamEntry[] entries; }

    [System.Serializable]
    class FxParamEntry
    {
        public string node;
        public string cls;
        public bool keepInitOffset, useSharedMaterial, protectMainUV, useSecondMap, protectSecondUV;
        public float xspeed, yspeed, secondXSpeed, secondYSpeed;
        public string secondMapName;
        public int tweenRepeat;
        public float tweenAnmTime;
        public FxExtraMap[] extras;
        // —— FxDelay 专用 ——
        public float delayTime;
        // —— FxUVRotation 专用（fxparams.json 里没有时保持默认值）——
        public string rotPropertyName;
        public bool rotateTex1, rotateTex2, rotateTex3, rotateTex4;
        public float angle1, angle2, angle3, angle4;
        public string raw;
    }

    [System.Serializable]
    class FxExtraMap
    {
        public string mapName;
        public float xSpeed, ySpeed, sx, sy, ox, oy;
    }

    static Dictionary<string, FxParamEntry[]> _params; // key = prefab 名小写
    static bool _paramsTried;

    static void LoadParams()
    {
        if (_paramsTried) return;
        _paramsTried = true;
        try
        {
            var p = Path.Combine(AbDir, "fxparams.json");
            if (!File.Exists(p))
            {
                Debug.LogWarning("[ArkFxLoader] 未找到 fxparams.json（UV 参数只能用兜底值）: " + p);
                return;
            }
            var doc = JsonUtility.FromJson<FxParamFile>(File.ReadAllText(p));
            if (doc?.prefabs == null)
            {
                Debug.LogWarning("[ArkFxLoader] fxparams.json 内容为空或 prefabs 字段缺失: " + p);
                return;
            }
            _params = new Dictionary<string, FxParamEntry[]>(doc.prefabs.Length);
            foreach (var pf in doc.prefabs)
                if (!string.IsNullOrEmpty(pf?.prefab))
                    _params[pf.prefab.ToLowerInvariant()] = pf.entries;
            Debug.Log($"[ArkFxLoader] fxparams.json 载入 {doc.prefabs.Length} 个 prefab 的原版脚本参数");
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[ArkFxLoader] fxparams.json 解析失败: " + e.Message);
        }
    }

    /// <summary>全局特效亮度系数：1=原版；&lt;1 压暗（无后处理环境下观感用）</summary>
    public static float Brightness = 1f;

    /// <summary>
    /// 是否使用 IDA 实证版（v6）的 UV 替身组件。
    /// true  = ArkFxUVTweenV6 / ArkFxUVTweenAdvanceV6 / ArkFxUVRotation（推荐）
    /// false = 旧版 ArkFxUVTween（v5，_UVTween 理论，仅用于 A/B 对比与回滚）
    /// </summary>
    // ---- 验收开关：用 PlayerPrefs 持久化，避免"重新进 Play 就失效" ----
    const string PrefUseV6 = "ArkFx.UseV6";
    const string PrefForceAb = "ArkFx.ForceAbSource";

    static bool _useV6 = true;
    static bool _forceAbSource;

    /// <summary>
    /// 是否使用 IDA 实证版（v6）的 UV 替身组件。
    /// true  = ArkFxUVTweenV6 / ArkFxUVTweenAdvanceV6 / ArkFxUVRotation（推荐）
    /// false = 旧版 ArkFxUVTween（v5，_UVTween 理论，仅用于 A/B 对比与回滚）
    /// </summary>
    public static bool UseV6
    {
        get { return _useV6; }
        set { _useV6 = value; PlayerPrefs.SetInt(PrefUseV6, value ? 1 : 0); PlayerPrefs.Save(); }
    }

    /// <summary>
    /// 强制走 AB 特效（跳过工程内 Assets/Bundles/Effect 的同名 prefab）。
    /// 验收用：工程内已有 414 个同名 prefab，默认情况下 AB 分支永远不会被命中。
    /// 编辑器菜单 Tools/ArkFx/0 可切换；值存在 PlayerPrefs 里，重进 Play 仍然有效。
    /// </summary>
    public static bool ForceAbSource
    {
        get { return _forceAbSource; }
        set { _forceAbSource = value; PlayerPrefs.SetInt(PrefForceAb, value ? 1 : 0); PlayerPrefs.Save(); }
    }

    /// <summary>最早时机恢复验收开关（SubsystemRegistration 早于 ArkFxBootstrap 的 BeforeSceneLoad）。</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void RestoreDebugSwitches()
    {
        _useV6 = PlayerPrefs.GetInt(PrefUseV6, 1) != 0;
        _forceAbSource = PlayerPrefs.GetInt(PrefForceAb, 0) != 0;
    }

    /// <summary>
    /// 游戏特效机的替身：实例化后把所有粒子和动画"踢起来"。
    /// 方舟 prefab 内的驱动脚本（Missing Script）在裸 Instantiate 下不会运行，
    /// 只有 playOnAwake 的粒子会播；playOnAwake=false 的子系统和
    /// Animator/AnimationClip 驱动的动效（如 sfsui 的火）必须在这里手动启动。
    /// </summary>
    public static void Kick(GameObject fx, string prefabName = null)
    {
        if (fx == null) return;
        ApplyFxParams(fx, prefabName);

        // 生命周期骨架：挂宿主并统一驱动（粒子播放、行为组件 OnPlay、结束判定都在宿主里）
        var host = ArkFxEffectHost.Ensure(fx);
        if (host != null)
        {
            host.Refresh();
            host.Play();
        }
        else
        {
            // 兜底：宿主创建失败时仍保证粒子能播
            foreach (var ps in fx.GetComponentsInChildren<ParticleSystem>(true))
                if (ps != null && !ps.isPlaying) ps.Play(true);
        }
        ApplyBrightness(fx);
        foreach (var anim in fx.GetComponentsInChildren<Animator>(true))
        {
            if (anim == null) continue;
            anim.enabled = true;
            anim.Rebind();
            // 按首个 clip 名 Play：默认状态名通常=clip 名；
            // 状态不存在时 Play 是静默 no-op，不会报错
            var clips = anim.runtimeAnimatorController != null
                ? anim.runtimeAnimatorController.animationClips : null;
            if (clips != null && clips.Length > 0)
                anim.Play(clips[0].name);
            anim.Update(0f);
        }
        ApplyMissingScriptFallback(fx);
    }

    /// <summary>
    /// fxparams 里的 node 路径是“prefab根名/子节点/…”的完整路径，
    /// 而 Transform.Find 是相对路径——先原样找，带根名前缀时剥掉第一段再找。
    /// </summary>
    static Transform FindNode(GameObject fx, string path)
    {
        if (string.IsNullOrEmpty(path)) return fx.transform;
        var t = fx.transform.Find(path);
        if (t != null) return t;
        var rootName = fx.name;
        if (!string.IsNullOrEmpty(rootName) && path.StartsWith(rootName + "/"))
            t = fx.transform.Find(path.Substring(rootName.Length + 1));
        return t;
    }

    // ================= 按名加载（表驱动兜底：EffectData / BulletData 用） =================

    /// <summary>
    /// 按 prefab 名在所有已登记的 arkfx 包里找特效，返回“prefab 资产”（不实例化），
    /// 供 EffectManager/BulletManager 的对象池当克隆模板。
    /// 驱动组件（UV 替身/原版脚本存档/亮度压暗/缺脚本兜底）会烘焙进 prefab 资产，
    /// 池化出的克隆自动继承；实例化侧再调 Kick 只负责“踢”（粒子/动画器）。
    /// </summary>
    public static GameObject LoadPrefabByName(string prefabName)
    {
        prefabName = (prefabName ?? "").Trim();
        if (string.IsNullOrEmpty(prefabName)) return null;
        if (!_loaded) LoadAll(AbDir);

        GameObject prefab = null;
        if (_loaded)
            foreach (var b in _bundles)
            {
                if (b == null) continue;
                prefab = TryLoadPrefab(b, prefabName);
                if (prefab != null) break;
            }
        if (prefab == null)
        {
            Debug.LogError($"[ArkFxLoader] 所有 arkfx 包里都没找到 prefab: {prefabName}");
            return null;
        }

        BakePrefab(prefab, prefabName);
        //if (ForceAbSource)
        //    Debug.Log($"[ArkFx] 命中 AB 特效: {prefabName}（来源包 {prefab.name}）");
        return prefab;
    }

    /// <summary>短名优先、完整资产路径兜底的 prefab 查找（静默，不打印诊断）</summary>
    static GameObject TryLoadPrefab(AssetBundle ab, string prefabName)
    {
        var p = ab.LoadAsset<GameObject>(prefabName);
        if (p != null) return p;
        foreach (var n in ab.GetAllAssetNames())
        {
            if (!n.EndsWith(".prefab")) continue;
            if (string.Equals(Path.GetFileNameWithoutExtension(n), prefabName,
                              System.StringComparison.OrdinalIgnoreCase))
            {
                p = ab.LoadAsset<GameObject>(n);
                if (p != null) return p;
            }
        }
        return null;
    }

    /// <summary>把驱动组件一次性烘焙进 prefab 资产；根节点打 _baked 标记，防止实例 Kick 时重复压亮度</summary>
    static void BakePrefab(GameObject prefab, string prefabName)
    {
        ApplyFxParams(prefab, prefabName);
        ApplyMissingScriptFallback(prefab);
        ApplyBrightness(prefab);
        ArkFxEffectHost.Ensure(prefab);
        if (prefab.GetComponent<ArkFxScriptStub>() == null)
        {
            var marker = prefab.AddComponent<ArkFxScriptStub>();
            marker.className = "_baked";
        }
    }

    // ================= 原版脚本替身的挂载（v5 / v6 双轨） =================

    /// <summary>挂 IDA 实证版 UV 滚动（FxUVTweener / FxUVTweenerAdvance）。</summary>
    static void AttachTweenV6(GameObject go, FxParamEntry e)
    {
        bool isAdvance = e.cls == "FxUVTweenerAdvance";
        if (isAdvance)
        {
            var tw = go.GetComponent<ArkFxUVTweenAdvanceV6>();
            if (tw == null) tw = go.AddComponent<ArkFxUVTweenAdvanceV6>();
            tw.keepInitOffset = e.keepInitOffset;
            tw.useSharedMaterial = e.useSharedMaterial;
            tw.mainSpeed = new Vector2(e.xspeed, e.yspeed);
            tw.useSecondMap = e.useSecondMap;
            tw.secondMapName = string.IsNullOrEmpty(e.secondMapName) ? "_DisturTex" : e.secondMapName;
            tw.secondSpeed = new Vector2(e.secondXSpeed, e.secondYSpeed);
            tw.tweenAnmTime = e.tweenAnmTime;
            tw.tweenRepeat = e.tweenRepeat != 0;      // 原版 tweenRepeat 是 bool
            if (e.extras != null)
            {
                tw.extraMaps = new List<ArkFxUVTweenAdvanceV6.MapSetting>(e.extras.Length);
                foreach (var m in e.extras)
                    if (m != null && !string.IsNullOrEmpty(m.mapName))
                        tw.extraMaps.Add(new ArkFxUVTweenAdvanceV6.MapSetting {
                            enabled = true,
                            mapName = m.mapName,
                            speed = new Vector2(m.xSpeed, m.ySpeed),
                            st = new Vector4(m.sx, m.sy, m.ox, m.oy)
                        });
            }
        }
        else
        {
            var tw = go.GetComponent<ArkFxUVTweenV6>();
            if (tw == null) tw = go.AddComponent<ArkFxUVTweenV6>();
            tw.keepInitOffset = e.keepInitOffset;
            tw.useSharedMaterial = e.useSharedMaterial;
            tw.protectMainUV = e.protectMainUV;
            tw.mainSpeed = new Vector2(e.xspeed, e.yspeed);
            tw.useSecondMap = e.useSecondMap;
            tw.protectSecondUV = e.protectSecondUV;
            tw.secondMapName = string.IsNullOrEmpty(e.secondMapName) ? "_DisturTex" : e.secondMapName;
            tw.secondSpeed = new Vector2(e.secondXSpeed, e.secondYSpeed);
        }
    }

    /// <summary>挂旧版 UV 滚动（仅 A/B 对比与回滚用，语义已被证伪）。</summary>
    static void AttachTweenV5(GameObject go, FxParamEntry e)
    {
        var tw = go.GetComponent<ArkFxUVTween>();
        if (tw == null) tw = go.AddComponent<ArkFxUVTween>();
        tw.keepInitOffset = e.keepInitOffset;
        tw.useSharedMaterial = e.useSharedMaterial;
        tw.protectMainUV = e.protectMainUV;
        tw.mainSpeed = new Vector2(e.xspeed, e.yspeed);
        tw.useSecondMap = e.useSecondMap;
        tw.protectSecondUV = e.protectSecondUV;
        tw.secondMapName = string.IsNullOrEmpty(e.secondMapName) ? "_DisturTex" : e.secondMapName;
        tw.secondSpeed = new Vector2(e.secondXSpeed, e.secondYSpeed);
        tw.duration = e.cls == "FxUVTweenerAdvance" ? e.tweenAnmTime : 0f;
        if (e.extras != null)
        {
            tw.extras = new List<ArkFxUVTween.ExtraMap>(e.extras.Length);
            foreach (var m in e.extras)
                if (m != null && !string.IsNullOrEmpty(m.mapName))
                    tw.extras.Add(new ArkFxUVTween.ExtraMap {
                        mapName = m.mapName,
                        speed = new Vector2(m.xSpeed, m.ySpeed),
                        st = new Vector4(m.sx, m.sy, m.ox, m.oy)
                    });
        }
    }

    /// <summary>挂延迟激活替身（FxDelay）。</summary>
    static void AttachDelay(GameObject go, FxParamEntry e)
    {
        var d = go.GetComponent<ArkFxDelay>();
        if (d == null) d = go.AddComponent<ArkFxDelay>();
        d.delayTime = e.delayTime;
    }

    /// <summary>挂 UV 旋转替身（FxUVRotation）。</summary>
    static void AttachRotation(GameObject go, FxParamEntry e)
    {
        var rot = go.GetComponent<ArkFxUVRotation>();
        if (rot == null) rot = go.AddComponent<ArkFxUVRotation>();
        rot.propertyName = string.IsNullOrEmpty(e.rotPropertyName) ? "_Rotation" : e.rotPropertyName;
        rot.rotateTex1 = e.rotateTex1;
        rot.rotateTex2 = e.rotateTex2;
        rot.rotateTex3 = e.rotateTex3;
        rot.rotateTex4 = e.rotateTex4;
        rot.angle1 = e.angle1;
        rot.angle2 = e.angle2;
        rot.angle3 = e.angle3;
        rot.angle4 = e.angle4;
    }

    /// <summary>按 fxparams.json 把原版缺脚本组件还原到各节点（幂等：已有组件不重复加）</summary>
    static void ApplyFxParams(GameObject fx, string prefabName)
    {
        if (fx == null || string.IsNullOrEmpty(prefabName)) return;
        LoadParams();
        if (_params == null
            || !_params.TryGetValue(prefabName.Trim().ToLowerInvariant(), out var entries)
            || entries == null) return;

        int missed = 0;
        foreach (var e in entries)
        {
            var node = FindNode(fx, e.node);
            if (node == null)
            {
                missed++;
                if (missed <= 3)
                    Debug.LogWarning($"[ArkFxLoader] '{prefabName}' 参数节点没找到: '{e.node}' ({e.cls})");
                continue;
            }

            if (e.cls == "FxUVTweener" || e.cls == "FxUVTweenerAdvance")
            {
                if (UseV6) AttachTweenV6(node.gameObject, e);
                else AttachTweenV5(node.gameObject, e);
            }
            else if (e.cls == "FxUVRotation")
            {
                AttachRotation(node.gameObject, e);
            }
            else if (e.cls == "FxDelay")
            {
                AttachDelay(node.gameObject, e);
            }
            else if (node.gameObject.GetComponent<ArkFxScriptStub>() == null)
            {
                // 其余原版脚本（ParticleEffect/ProjectileEffect 等）先存档备查
                var stub = node.gameObject.AddComponent<ArkFxScriptStub>();
                stub.className = e.cls;
                stub.rawJson = e.raw;
            }
        }
        if (missed > 3)
            Debug.LogWarning($"[ArkFxLoader] '{prefabName}' 另有 {missed - 3} 条参数节点没找到（多为节点路径不匹配，不影响其余特效）");
    }

    /// <summary>从源头压亮度：粒子起始色/材质 _Color × 亮度系数；已烘焙资产（_baked 标记）跳过防重复压暗</summary>
    static void ApplyBrightness(GameObject fx)
    {
        if (fx == null || Brightness >= 1f) return;
        var marker = fx.GetComponent<ArkFxScriptStub>();
        if (marker != null && marker.className == "_baked") return;

        foreach (var ps in fx.GetComponentsInChildren<ParticleSystem>(true))
        {
            var main = ps.main;
            var sc = main.startColor;
            switch (sc.mode)
            {
                case ParticleSystemGradientMode.Color:
                    sc.color = sc.color * Brightness;
                    break;
                case ParticleSystemGradientMode.TwoColors:
                    sc.colorMin = sc.colorMin * Brightness;
                    sc.colorMax = sc.colorMax * Brightness;
                    break;
                // Gradient/TwoGradients 是颜色曲线，无法直接乘系数，交给材质 _Color 兜底
            }
            main.startColor = sc;
        }
        foreach (var r in fx.GetComponentsInChildren<Renderer>(true))
        {
            var mats = r.materials;
            bool dirty = false;
            foreach (var m in mats)
            {
                if (m != null && m.HasProperty("_Color"))
                {
                    m.SetColor("_Color", m.GetColor("_Color") * Brightness);
                    dirty = true;
                }
            }
            if (dirty) r.materials = mats;
        }
    }

    /// <summary>缺脚本节点补身：带 Renderer 的 Missing Script 节点是原版 FxUVTweener，补挂替身组件</summary>
    static void ApplyMissingScriptFallback(GameObject fx)
    {
        foreach (var t in fx.GetComponentsInChildren<Transform>(true))
        {
            bool hasMissing = false;
            foreach (var c in t.gameObject.GetComponents<Component>())
                if (c == null) { hasMissing = true; break; } // null 占位 = Missing Script
            if (!hasMissing) continue;
            if (t.GetComponent<Renderer>() == null) continue;
            // 幂等：任何 ArkFx 替身组件存在就跳过，否则每次 Kick（含对象池复用）
            // 都会重复 AddComponent，导致 UV 速度按次叠加
            if (t.GetComponent<ArkFxUVTween>() != null) continue;
            if (t.GetComponent<ArkFxUVTweenV6>() != null) continue;
            if (t.GetComponent<ArkFxUVTweenAdvanceV6>() != null) continue;
            if (t.GetComponent<ArkFxUVRotation>() != null) continue;
            if (t.GetComponent<ArkFxScriptStub>() != null) continue;
            {
                // 无参数兜底：用 sfsui 系列逆出的典型值，别留 0（0 = 完全不动）
                if (UseV6)
                {
                    var tw6 = t.gameObject.AddComponent<ArkFxUVTweenV6>();
                    tw6.mainSpeed = new Vector2(0f, -0.25f);
                }
                else
                {
                    var tw = t.gameObject.AddComponent<ArkFxUVTween>();
                    tw.mainSpeed = new Vector2(0f, -0.25f);
                }
            }
        }
    }

    public static IEnumerable<string> ListEffects(string abPath)
    {
        var ab = AssetBundle.LoadFromFile(abPath);
        if (ab == null) yield break;
        foreach (var n in ab.GetAllAssetNames())
            if (n.EndsWith(".prefab")) yield return Path.GetFileNameWithoutExtension(n);
    }
}
