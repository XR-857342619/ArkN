using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace DIY
{
    public partial class UI_Main
    {
        /// <summary>
        /// 刷新 StreamingAssets/Data 下的配置文件夹列表。
        /// </summary>
        private void RefreshFolderList()
        {
            folderList.Clear();
            m_folders.items = new string[0];
            folder = "";

            if (!Directory.Exists(dataRootPath))
            {
                m_tip.text = m_tip.text + "\n配置目录不存在: " + dataRootPath;
                ClearUnitList();
                return;
            }

            folderList = Directory.GetDirectories(dataRootPath)
                .Select(Path.GetFileName)
                .Where(x => !string.IsNullOrEmpty(x))
                .OrderBy(x => x, StringComparer.Ordinal)
                .ToList();

            m_folders.items = folderList.ToArray();

            if (folderList.Count == 0)
            {
                m_tip.text = m_tip.text + "\n配置目录下没有文件夹: " + dataRootPath;
                ClearUnitList();
                return;
            }

            // 默认优先选中 UnitData；没有时退回第一个配置文件夹。
            folder = folderList.Contains("UnitData") ? "UnitData" : folderList[0];
            m_folders.selectedIndex = folderList.IndexOf(folder);
            freshJsonFiles(folder);
        }

        /// <summary>
        /// 读取指定配置文件夹下的 JSON 文件，排除 _index.txt / _index.json 与 .meta。
        /// </summary>
        private void freshJsonFiles(string folderName)
        {
            jsonFilePath = "";
            m_jsonFiles.items = new string[0];
            ClearUnitList();

            if (string.IsNullOrEmpty(folderName)) return;

            string path = Path.Combine(dataRootPath, folderName);
            if (!Directory.Exists(path))
            {
                m_tip.text = m_tip.text + "\n配置文件夹不存在: " + path;
                return;
            }

            string[] names = Directory.GetFiles(path)
                .Select(Path.GetFileName)
                .Where(name =>
                    !string.IsNullOrEmpty(name) &&
                    !name.EndsWith(".meta", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(name, "_index.txt", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(name, "_index.json", StringComparison.OrdinalIgnoreCase) &&
                    (name.EndsWith(".txt", StringComparison.OrdinalIgnoreCase) ||
                     name.EndsWith(".json", StringComparison.OrdinalIgnoreCase)))
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();

            m_jsonFiles.items = names;

            if (names.Length == 0)
            {
                m_tip.text = m_tip.text + "\n" + folderName + " 下没有可用的 JSON/TXT 配置文件";
                return;
            }

            // 0.txt 是 UnitData 的占位文件，默认跳过它，方便进入页面后立即看到真实单位。
            int defaultIndex = Array.FindIndex(
                names,
                name => !string.Equals(name, "0.txt", StringComparison.OrdinalIgnoreCase));
            if (defaultIndex < 0)
                defaultIndex = 0;

            m_jsonFiles.selectedIndex = defaultIndex;
            OnJsonFileChanged();
        }

        private void OnJsonFileChanged()
        {
            if (m_jsonFiles.items == null || m_jsonFiles.items.Length == 0)
            {
                jsonFilePath = "";
                ClearUnitList();
                return;
            }

            int index = Mathf.Clamp(m_jsonFiles.selectedIndex, 0, m_jsonFiles.items.Length - 1);
            jsonFilePath = Path.Combine(dataRootPath, folder, m_jsonFiles.items[index]);
            LoadUnitDataFromJson(jsonFilePath);
        }

        /// <summary>
        /// 从 JSON 文本加载 UnitData。
        /// 同时兼容：
        /// - 每行一个 JSON 对象（当前 Data/&lt;Table&gt;/*.txt 格式）
        /// - 标准 JSON 数组（后续纯 JSON 格式）
        /// </summary>
        private void LoadUnitDataFromJson(string path)
        {
            ClearUnitList();

            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                m_tip.text = m_tip.text + "\n配置文件不存在: " + path;
                return;
            }

            var tokens = new List<JToken>();

            try
            {
                string text = File.ReadAllText(path);

                // 先按完整 JSON 解析，支持 JSON 数组或单个对象
                try
                {
                    JToken root = JToken.Parse(text);
                    if (root is JArray array)
                        tokens.AddRange(array);
                    else
                        tokens.Add(root);
                }
                catch
                {
                    // JSONL：每行一个 JSON 对象
                    foreach (string line in text.Split('\n'))
                    {
                        if (string.IsNullOrWhiteSpace(line)) continue;

                        try
                        {
                            tokens.Add(JToken.Parse(line));
                        }
                        catch (Exception e)
                        {
                            Debug.LogWarning($"[DIY] 解析配置文件行失败: {path}\n{line}\n{e.Message}");
                        }
                    }
                }

                int index = 0;
                foreach (var token in tokens)
                {
                    if (token is not JObject obj) continue;

                    UnitData unit = null;
                    try
                    {
                        unit = JsonHelper.FromJson<UnitData>(obj.ToString());
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning($"[DIY] UnitData 反序列化失败: {path}\n{obj}\n{e.Message}");
                    }

                    if (unit == null || string.IsNullOrEmpty(unit.Id))
                        continue;

                    if (!unitDataById.ContainsKey(unit.Id))
                        currentUnitDatas.Add(unit);
                    unitDataById[unit.Id] = unit;
                    unitIndexs[unit.Id] = index;

                    string unitName = string.IsNullOrEmpty(unit.Name) ? unit.Id : unit.Name;
                    unitInfos[(unitName + "/" + unit.Id, unit.Type ?? "", unit.HeadIcon ?? "")] = index;
                    index++;
                }

                lastRow = currentUnitDatas.Count;
                m_tip.text = m_tip.text + "\n已加载 " + currentUnitDatas.Count + " 个单位: " + Path.GetFileName(path);
                searchItem();
            }
            catch (Exception e)
            {
                Debug.LogError($"[DIY] 读取配置失败: {path}\n{e}");
                m_tip.text = m_tip.text + "\n读取失败: " + e.Message;
            }
        }

        private void ClearUnitList()
        {
            currentUnitDatas.Clear();
            unitDataById.Clear();
            unitInfos.Clear();
            unitIndexs.Clear();
            selectUnit = null;
            lastRow = 0;
            m_selectUnitCombobox.items = new string[0];
            ClearUnitInfoFields();
        }

        private void ClearUnitInfoFields()
        {
            m_unitName.m_text.text = "";
            m_modeName.m_text.text = "";
            m_HP.m_text.text = "";
            m_Def.m_text.text = "";
            m_MicDef.m_text.text = "";
            m_Atk.m_text.text = "";
            m_Cost.m_text.text = "";
            m_OpStopCount.m_text.text = "";
            m_EnStopCount.m_text.text = "";
            m_OpAtkGap.m_text.text = "";
            m_EnAtkGap.m_text.text = "";
            m_ResetTime.m_text.text = "";
            m_Weight.m_text.text = "";
            m_NotUseTile.m_bool.selected = false;
        }

        private void OnSelectedUnitChanged()
        {
            if (!TryGetSelectedUnit(out UnitData unit))
            {
                selectUnit = null;
                ClearUnitInfoFields();
                return;
            }

            selectUnit = unit;
            string modelName = unit.Model ?? "";
            string icon = unit.HeadIcon ?? "";

            m_unitName.m_text.text = string.IsNullOrEmpty(unit.Name) ? unit.Id : unit.Name;
            m_modeName.m_text.text = modelName;
            m_mode.alpha = string.IsNullOrEmpty(modelName) ? 1 : 0;

            if (!string.IsNullOrEmpty(modelName))
                loadModel(modelName, out _);

            if (m_unitType.selectedIndex != 0 && !string.IsNullOrEmpty(icon))
                m_unitIcon.url = "ui://Res/" + icon;
            else
                m_unitIcon.url = "ui://Res/头像_装置_障碍物";

            m_HP.m_text.text = unit.Hp.ToString();
            m_Def.m_text.text = unit.Defence.ToString();
            m_MicDef.m_text.text = unit.MagicDefence.ToString();
            m_Atk.m_text.text = unit.Attack.ToString();
            m_Cost.m_text.text = unit.Cost.ToString();
            m_OpStopCount.m_text.text = unit.StopCount.ToString();
            m_EnStopCount.m_text.text = unit.StopCount.ToString();
            m_OpAtkGap.m_text.text = unit.AttackGap.ToString();
            m_EnAtkGap.m_text.text = unit.AttackGap.ToString();
            m_ResetTime.m_text.text = unit.ResetTime.ToString();
            m_Weight.m_text.text = unit.Weight.ToString();
            m_NotUseTile.m_bool.selected = unit.NotUseTile;
        }

        private bool TryGetSelectedUnit(out UnitData unit)
        {
            unit = null;
            if (m_selectUnitCombobox.items == null || m_selectUnitCombobox.items.Length == 0)
                return false;

            int index = Mathf.Clamp(m_selectUnitCombobox.selectedIndex, 0, m_selectUnitCombobox.items.Length - 1);
            string key = m_selectUnitCombobox.items[index];
            int slashIndex = key.LastIndexOf('/');
            string id = slashIndex >= 0 ? key.Substring(slashIndex + 1) : key;

            return unitDataById.TryGetValue(id, out unit) && unit != null;
        }

        private void searchItem()
        {
            if (currentUnitDatas.Count == 0)
            {
                m_selectUnitCombobox.items = new string[0];
                selectUnit = null;
                return;
            }

            int typeIndex = Mathf.Clamp(m_unitType.selectedIndex, 0, unitTypes.Length - 1);
            string targetType = unitTypes[typeIndex];

            IEnumerable<UnitData> query = currentUnitDatas
                .Where(unit => unit != null && unit.Type == targetType);

            string keyword = m_unitNameInput.text ?? "";
            if (!string.IsNullOrEmpty(keyword))
                query = query.Where(unit => (unit.Name ?? unit.Id ?? "").Contains(keyword));

            string[] items = query
                .Select(unit => (string.IsNullOrEmpty(unit.Name) ? unit.Id : unit.Name) + "/" + unit.Id)
                .ToArray();

            m_selectUnitCombobox.items = items;

            if (items.Length == 0)
            {
                selectUnit = null;
                ClearUnitInfoFields();
                return;
            }

            m_selectUnitCombobox.selectedIndex = 0;
            OnSelectedUnitChanged();
        }
    }
}
