using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace DiyEditor
{
    /// <summary>
    /// DIY 配置字段 JSON 的根对象。
    /// 由编辑器工具生成，运行时供 DIY 编辑器读取，用于构建属性列表。
    /// </summary>
    [Serializable]
    public class DiySchemaFile
    {
        public int version = 1;
        public string sourceFolder = "Assets/Scripts/Config";
        public List<DiySchemaClass> classes = new List<DiySchemaClass>();
    }

    /// <summary>
    /// 一个配置类对应一个分页。
    /// </summary>
    [Serializable]
    public class DiySchemaClass
    {
        public string className;
        public string sourceFile;

        /// <summary>是否禁用此配置类（废弃项）。</summary>
        public bool disabled;

        public List<DiySchemaField> fields = new List<DiySchemaField>();
    }

    /// <summary>
    /// 配置类中的一个字段。
    /// </summary>
    [Serializable]
    public class DiySchemaField
    {
        public string name;
        public string type;

        /// <summary>中文备注/显示名。</summary>
        public string label;

        /// <summary>是否是 DIY 页面自动填充的默认属性。</summary>
        public bool isDefault;

        /// <summary>字段分组名。</summary>
        public string group;
    }

    /// <summary>
    /// 读取 Assets/Scripts/Config 下所有实现 IConfig 的配置类，生成供 DIY 编辑器使用的字段 JSON。
    /// </summary>
    public class DiySchemaGeneratorWindow : EditorWindow
    {
        private const string ConfigFolder = "Assets/Scripts/Config";
        private const string DefaultOutputPath = "Assets/Bundles/Data/DIY/DiySchema.json";
        private const string OutputPathPrefKey = "DiySchemaGenerator.OutputPath";

        private string outputPath = DefaultOutputPath;
        private DiySchemaFile schema = new DiySchemaFile();
        private int selectedClassIndex = 0;
        private Vector2 classScroll;
        private Vector2 fieldScroll;
        private bool dirty;
        private string lastSavedTime = "";

        private class ConfigFieldDescriptor
        {
            public string Name;
            public string TypeName;
            public int DeclarationOrder;
        }

        private class ConfigTypeDescriptor
        {
            public Type Type;
            public string AssetPath;
            public List<ConfigFieldDescriptor> Fields;
        }

        [MenuItem("Tools/DIY/配置字段JSON生成器")]
        public static void Open()
        {
            var window = GetWindow<DiySchemaGeneratorWindow>("DIY 配置字段");
            window.minSize = new Vector2(980, 540);
            window.Show();
        }

        private void OnEnable()
        {
            outputPath = EditorPrefs.GetString(OutputPathPrefKey, DefaultOutputPath);
            ReloadFromDisk();
        }

        private void OnDisable()
        {
            EditorPrefs.SetString(OutputPathPrefKey, outputPath);
        }

        private void OnGUI()
        {
            DrawToolbar();
            EditorGUILayout.Space();

            if (schema == null || schema.classes == null || schema.classes.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "未在 " + ConfigFolder + " 中找到实现 IConfig 的配置类。",
                    MessageType.Warning);
                return;
            }

            selectedClassIndex = Mathf.Clamp(selectedClassIndex, 0, schema.classes.Count - 1);

            EditorGUILayout.BeginHorizontal();
            DrawClassList();
            DrawSelectedClassPage();
            EditorGUILayout.EndHorizontal();

            DrawFooter();
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            GUILayout.Label("输出路径", GUILayout.Width(56));
            outputPath = EditorGUILayout.TextField(outputPath);

            if (GUILayout.Button("浏览", EditorStyles.toolbarButton, GUILayout.Width(48)))
                BrowseOutputPath();

            if (GUILayout.Button("重新载入", EditorStyles.toolbarButton, GUILayout.Width(64)))
            {
                if (dirty && !EditorUtility.DisplayDialog(
                        "重新载入",
                        "当前有未保存的修改，重新载入会丢失这些修改，是否继续？",
                        "继续",
                        "取消"))
                {
                    return;
                }

                ReloadFromDisk();
            }

            if (GUILayout.Button(dirty ? "保存 *" : "保存", EditorStyles.toolbarButton, GUILayout.Width(64)))
                SaveToDisk();

            EditorGUILayout.EndHorizontal();
        }

        private void DrawClassList()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(230));

            EditorGUILayout.LabelField("配置类分页", EditorStyles.boldLabel);
            classScroll = EditorGUILayout.BeginScrollView(classScroll);

            for (int i = 0; i < schema.classes.Count; i++)
            {
                var configClass = schema.classes[i];
                if (configClass == null) continue;

                string title = configClass.className;
                if (configClass.disabled)
                    title += "  [已禁用]";

                var oldColor = GUI.backgroundColor;
                if (i == selectedClassIndex)
                    GUI.backgroundColor = new Color(0.45f, 0.72f, 1f);

                if (GUILayout.Button(title, GUILayout.Height(24)))
                {
                    selectedClassIndex = i;
                    fieldScroll = Vector2.zero;
                    GUI.FocusControl(null);
                }

                GUI.backgroundColor = oldColor;
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawSelectedClassPage()
        {
            var configClass = schema.classes[selectedClassIndex];
            if (configClass == null) return;

            EditorGUILayout.BeginVertical("box", GUILayout.ExpandWidth(true));

            EditorGUILayout.LabelField(configClass.className, EditorStyles.boldLabel);
            EditorGUILayout.LabelField("源文件", configClass.sourceFile ?? "");

            EditorGUI.BeginChangeCheck();
            bool disabled = EditorGUILayout.ToggleLeft(
                "禁用此配置类（废弃项）",
                configClass.disabled);
            if (EditorGUI.EndChangeCheck())
            {
                configClass.disabled = disabled;
                MarkDirty();
            }

            if (configClass.disabled)
            {
                EditorGUILayout.HelpBox(
                    "此配置类已被标记为废弃/禁用。下方的字段配置仍会保留，但不会作为 DIY 的可用分页。",
                    MessageType.Info);
            }

            EditorGUILayout.Space();

            DrawFieldHeader();

            fieldScroll = EditorGUILayout.BeginScrollView(
                fieldScroll,
                GUILayout.ExpandHeight(true),
                GUILayout.ExpandWidth(true));

            if (configClass.fields == null)
                configClass.fields = new List<DiySchemaField>();

            EditorGUI.BeginChangeCheck();
            using (new EditorGUI.DisabledScope(configClass.disabled))
            {
                foreach (var field in configClass.fields)
                {
                    if (field == null) continue;
                    DrawFieldRow(field);
                }
            }

            if (EditorGUI.EndChangeCheck())
                MarkDirty();

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawFieldHeader()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            GUILayout.Label("字段名", EditorStyles.boldLabel, GUILayout.Width(190));
            GUILayout.Label("类型", EditorStyles.boldLabel, GUILayout.Width(170));
            GUILayout.Label("中文备注", EditorStyles.boldLabel, GUILayout.ExpandWidth(true));
            GUILayout.Label("默认属性", EditorStyles.boldLabel, GUILayout.Width(70));
            GUILayout.Label("分组", EditorStyles.boldLabel, GUILayout.ExpandWidth(true));

            EditorGUILayout.EndHorizontal();
        }

        private void DrawFieldRow(DiySchemaField field)
        {
            EditorGUILayout.BeginHorizontal();

            GUILayout.Label(field.name ?? "", GUILayout.Width(190));
            GUILayout.Label(field.type ?? "", EditorStyles.miniLabel, GUILayout.Width(170));

            field.label = EditorGUILayout.TextField(field.label ?? "", GUILayout.ExpandWidth(true));
            field.isDefault = EditorGUILayout.Toggle(field.isDefault, GUILayout.Width(70));
            field.group = EditorGUILayout.TextField(field.group ?? "", GUILayout.ExpandWidth(true));

            EditorGUILayout.EndHorizontal();
        }

        private void DrawFooter()
        {
            int totalFields = schema.classes
                .Where(c => c != null && c.fields != null)
                .Sum(c => c.fields.Count);

            int defaultFields = schema.classes
                .Where(c => c != null && c.fields != null)
                .SelectMany(c => c.fields)
                .Count(f => f != null && f.isDefault);

            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            GUILayout.Label($"配置类: {schema.classes.Count}    字段总数: {totalFields}    默认属性: {defaultFields}");
            GUILayout.FlexibleSpace();
            GUILayout.Label(dirty ? "有未保存修改" : (string.IsNullOrEmpty(lastSavedTime) ? "" : "已保存: " + lastSavedTime));
            EditorGUILayout.EndHorizontal();
        }

        /// <summary>
        /// 重新扫描配置类，并与已有 JSON 合并：
        /// - 已有字段保留中文备注、默认属性、分组；
        /// - 新增字段自动追加；
        /// - 已不存在的字段会被移除；
        /// - 已禁用状态会保留。
        /// </summary>
        private void ReloadFromDisk()
        {
            var descriptors = DiscoverConfigTypes();

            DiySchemaFile existing = null;
            if (File.Exists(outputPath))
            {
                try
                {
                    string json = File.ReadAllText(outputPath, Encoding.UTF8);
                    existing = JsonUtility.FromJson<DiySchemaFile>(json);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[DiySchemaGenerator] 读取已有 JSON 失败，将重新生成: {outputPath}\n{e}");
                }
            }
            else
            {
                Debug.Log($"[DiySchemaGenerator] 未找到已有 JSON，将新建: {outputPath}");
            }

            var existingClassMap = new Dictionary<string, DiySchemaClass>(StringComparer.Ordinal);
            if (existing != null && existing.classes != null)
            {
                foreach (var configClass in existing.classes)
                {
                    if (configClass == null || string.IsNullOrEmpty(configClass.className))
                        continue;

                    existingClassMap[configClass.className] = configClass;
                }
            }

            var newSchema = new DiySchemaFile
            {
                version = existing?.version > 0 ? existing.version : 1,
                sourceFolder = ConfigFolder,
                classes = new List<DiySchemaClass>()
            };

            foreach (var descriptor in descriptors)
            {
                existingClassMap.TryGetValue(descriptor.Type.Name, out var oldClass);
                newSchema.classes.Add(MergeClass(descriptor, oldClass));
            }

            schema = newSchema;
            selectedClassIndex = Mathf.Clamp(selectedClassIndex, 0, Mathf.Max(0, schema.classes.Count - 1));
            dirty = false;

            Debug.Log($"[DiySchemaGenerator] 已载入 {schema.classes.Count} 个配置类（{outputPath}）");
        }

        private static DiySchemaClass MergeClass(ConfigTypeDescriptor descriptor, DiySchemaClass oldClass)
        {
            var result = new DiySchemaClass
            {
                className = descriptor.Type.Name,
                sourceFile = descriptor.AssetPath,
                disabled = oldClass != null && oldClass.disabled,
                fields = new List<DiySchemaField>()
            };

            var oldFieldMap = new Dictionary<string, DiySchemaField>(StringComparer.Ordinal);
            if (oldClass != null && oldClass.fields != null)
            {
                foreach (var oldField in oldClass.fields)
                {
                    if (oldField == null || string.IsNullOrEmpty(oldField.name))
                        continue;

                    oldFieldMap[oldField.name] = oldField;
                }
            }

            foreach (var fieldDescriptor in descriptor.Fields)
            {
                oldFieldMap.TryGetValue(fieldDescriptor.Name, out var oldField);

                result.fields.Add(new DiySchemaField
                {
                    name = fieldDescriptor.Name,
                    type = fieldDescriptor.TypeName,
                    label = oldField != null ? (oldField.label ?? "") : fieldDescriptor.Name,
                    isDefault = oldField != null && oldField.isDefault,
                    group = oldField?.group ?? ""
                });
            }

            return result;
        }

        private static List<ConfigTypeDescriptor> DiscoverConfigTypes()
        {
            var result = new List<ConfigTypeDescriptor>();

            if (!AssetDatabase.IsValidFolder(ConfigFolder))
            {
                Debug.LogError($"[DiySchemaGenerator] 配置目录不存在: {ConfigFolder}");
                return result;
            }

            string[] guids = AssetDatabase.FindAssets("t:MonoScript", new[] { ConfigFolder });
            var visitedTypes = new HashSet<Type>();

            foreach (string guid in guids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                if (!assetPath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
                    continue;

                var script = AssetDatabase.LoadAssetAtPath<MonoScript>(assetPath);
                var type = script != null ? script.GetClass() : null;
                if (type == null || !visitedTypes.Add(type))
                    continue;

                if (type.IsAbstract || type.IsInterface)
                    continue;

                if (!typeof(IConfig).IsAssignableFrom(type))
                    continue;

                result.Add(new ConfigTypeDescriptor
                {
                    Type = type,
                    AssetPath = assetPath,
                    Fields = CollectFields(type)
                });
            }

            return result
                .OrderBy(x => x.Type.Name, StringComparer.Ordinal)
                .ToList();
        }

        private static List<ConfigFieldDescriptor> CollectFields(Type type)
        {
            var descriptors = new List<ConfigFieldDescriptor>();

            foreach (var field in type.GetFields(
                         BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                if (field.IsLiteral || field.IsInitOnly)
                    continue;

                descriptors.Add(new ConfigFieldDescriptor
                {
                    Name = field.Name,
                    TypeName = GetFriendlyTypeName(field.FieldType),
                    DeclarationOrder = field.MetadataToken
                });
            }

            foreach (var property in type.GetProperties(
                         BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                if (!property.CanRead || !property.CanWrite)
                    continue;

                if (property.GetIndexParameters().Length > 0)
                    continue;

                descriptors.Add(new ConfigFieldDescriptor
                {
                    Name = property.Name,
                    TypeName = GetFriendlyTypeName(property.PropertyType),
                    DeclarationOrder = property.MetadataToken
                });
            }

            // 配置类一般 Id 为属性，其余为字段。把 Id 放到最前面，其余按源码声明顺序排列。
            return descriptors
                .OrderBy(x => x.Name == "Id" ? 0 : 1)
                .ThenBy(x => x.DeclarationOrder)
                .ToList();
        }

        private static string GetFriendlyTypeName(Type type)
        {
            if (type == null) return "";

            if (type.IsArray)
                return GetFriendlyTypeName(type.GetElementType()) + "[]";

            var nullableType = Nullable.GetUnderlyingType(type);
            if (nullableType != null)
                return GetFriendlyTypeName(nullableType) + "?";

            if (type == typeof(string)) return "string";
            if (type == typeof(object)) return "object";
            if (type == typeof(int)) return "int";
            if (type == typeof(long)) return "long";
            if (type == typeof(float)) return "float";
            if (type == typeof(double)) return "double";
            if (type == typeof(bool)) return "bool";

            if (type.IsGenericType)
            {
                string name = type.Name;
                int tickIndex = name.IndexOf('`');
                if (tickIndex >= 0)
                    name = name.Substring(0, tickIndex);

                string args = string.Join(",", type.GetGenericArguments().Select(GetFriendlyTypeName));
                return name + "<" + args + ">";
            }

            return type.Name;
        }

        private void BrowseOutputPath()
        {
            string directory = Path.GetDirectoryName(outputPath);
            if (string.IsNullOrEmpty(directory))
                directory = "Assets";

            string absolutePath = EditorUtility.SaveFilePanel(
                "选择配置字段 JSON 保存位置",
                directory,
                string.IsNullOrEmpty(Path.GetFileName(outputPath)) ? "DiySchema" : Path.GetFileNameWithoutExtension(outputPath),
                "json");

            if (string.IsNullOrEmpty(absolutePath))
                return;

            outputPath = ToProjectRelativePath(absolutePath);
            EditorPrefs.SetString(OutputPathPrefKey, outputPath);

            // 没有未保存修改时，切换路径后自动载入目标 JSON，方便继续编辑已有配置。
            if (!dirty)
                ReloadFromDisk();
        }

        private void SaveToDisk()
        {
            if (string.IsNullOrEmpty(outputPath))
            {
                EditorUtility.DisplayDialog("保存失败", "输出路径不能为空。", "确定");
                return;
            }

            try
            {
                if (schema == null)
                    schema = new DiySchemaFile();

                schema.version = 1;
                schema.sourceFolder = ConfigFolder;

                string directory = Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                string json = JsonUtility.ToJson(schema, true);
                File.WriteAllText(outputPath, json, new UTF8Encoding(false));

                AssetDatabase.Refresh();

                dirty = false;
                lastSavedTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                Debug.Log($"[DiySchemaGenerator] 已保存配置字段 JSON: {outputPath}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[DiySchemaGenerator] 保存失败: {outputPath}\n{e}");
                EditorUtility.DisplayDialog("保存失败", e.Message, "确定");
            }
        }

        private void MarkDirty()
        {
            dirty = true;
            Repaint();
        }

        private static string ToProjectRelativePath(string absolutePath)
        {
            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            if (string.IsNullOrEmpty(projectRoot))
                return absolutePath.Replace('\\', '/');

            string normalizedRoot = projectRoot.Replace('\\', '/').TrimEnd('/');
            string normalizedPath = absolutePath.Replace('\\', '/');

            if (normalizedPath.StartsWith(normalizedRoot + "/", StringComparison.OrdinalIgnoreCase))
                return normalizedPath.Substring(normalizedRoot.Length + 1);

            return normalizedPath;
        }
    }
}
