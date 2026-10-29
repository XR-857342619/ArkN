using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FairyGUI;
using UnityEngine;

namespace MapBuilderUI
{
    partial class UI_MapInfo
    {
        public MapInfo MapInfo
        {
            get
            {
                var builder = parent as UI_MapBuilder;
                return builder != null ? builder.MapInfo : null;
            }
        }

        // 打了 BGM 标签的全部 Addressables 地址（下拉框的 values）
        private string[] _bgmAddresses;
        private bool _bgmOptionsLoaded;

        partial void Init()
        {
            m_Contract.RemoveChildrenToPool();
            foreach (var contract in Database.Instance.GetAll<ContractData>())
            {
                if (contract == null) continue;
                var contractUI = m_Contract.AddItemFromPool() as UI_ContractIcon;
                contractUI.SetContract(contract);
            }
            m_Contract.onClickItem.Add(chooseContract);
            m_MapName.onChanged.Add(() => { MapInfo.MapName = m_MapName.text; });
            m_MapDesc.onChanged.Add(() => { MapInfo.Description = m_MapDesc.text; });
            m_InitHp.onChanged.Add(() => { MapInfo.InitHp = int.Parse(m_InitHp.text); });
            m_InitCost.onChanged.Add(() => { MapInfo.InitCost = int.Parse(m_InitCost.text); });
            m_BuildCount.onChanged.Add(() => { MapInfo.MaxBuildCount = int.Parse(m_BuildCount.text); });
            m_MaxCost.onChanged.Add(() => { MapInfo.MaxCost = int.Parse(m_MaxCost.text); });
            m_NoBuildLimit.onClick.Add(() => { MapInfo.NoBuildLimit = !MapInfo.NoBuildLimit; });
            m_BoxCount.onChanged.Add(() => { MapInfo.BoxCount = int.Parse(m_BoxCount.text); });

            // BGM 下拉框：显示文件名，实际值存 Addressables 完整地址
            m_SelectBGM.onChanged.Add(() =>
            {
                var mapInfo = MapInfo;
                if (mapInfo != null)
                    mapInfo.BGM = m_SelectBGM.value;
            });
            _ = LoadBgmOptionsAsync();
        }

        /// <summary>从 Addressables 的 BGM 标签读取全部可用 BGM，填充下拉框。</summary>
        private async Task LoadBgmOptionsAsync()
        {
            List<string> addresses;
            try
            {
                addresses = await ResHelper.GetAddressesByLabelAsync("BGM", typeof(AudioClip));
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"[UI_MapInfo] 读取 BGM 列表失败: {e}");
                return;
            }

            if (addresses == null || addresses.Count == 0)
            {
                UnityEngine.Debug.LogWarning("[UI_MapInfo] 没有找到打了 BGM 标签的音频资源，SelectBGM 下拉框将为空");
                return;
            }

            _bgmAddresses = addresses.ToArray();
            m_SelectBGM.items = _bgmAddresses.Select(a => System.IO.Path.GetFileName(a)).ToArray();
            m_SelectBGM.values = _bgmAddresses;
            _bgmOptionsLoaded = true;

            ApplyBgmSelection();
        }

        /// <summary>
        /// 把 MapInfo.BGM 同步到下拉框；未配置或配置的地址已失效时回退到第一项并回写。
        /// </summary>
        private void ApplyBgmSelection()
        {
            if (!_bgmOptionsLoaded) return;

            var mapInfo = MapInfo;
            if (mapInfo == null) return;

            int index = -1;
            if (!string.IsNullOrEmpty(mapInfo.BGM))
            {
                index = Array.IndexOf(_bgmAddresses, mapInfo.BGM);
                if (index < 0)
                    UnityEngine.Debug.LogWarning($"[UI_MapInfo] 地图配置的 BGM「{mapInfo.BGM}」不在 BGM 标签列表中，已回退到默认 BGM");
            }

            if (index < 0)
            {
                // 未配置（或配置失效）：优先使用默认 BGM（执棋者之骨），否则第一项
                index = Array.IndexOf(_bgmAddresses, AudioManager.DefaultBgmAddress);
                if (index < 0) index = 0;
            }

            m_SelectBGM.selectedIndex = index;
            // items / selectedIndex 的程序化赋值不会触发 onChanged，这里显式回写，保证 UI 与数据一致
            mapInfo.BGM = m_SelectBGM.value;
        }

        public void Fresh()
        {
            m_MapName.text = MapInfo.MapName;
            m_MapDesc.text = MapInfo.Description;
            m_InitHp.text = MapInfo.InitHp.ToString();
            m_InitCost.text = MapInfo.InitCost.ToString();
            m_BuildCount.text = MapInfo.MaxBuildCount.ToString();
            m_MaxCost.text = MapInfo.MaxCost.ToString();
            m_NoBuildLimit.selected = MapInfo.NoBuildLimit;
            m_BoxCount.text = MapInfo.BoxCount.ToString();
            ApplyBgmSelection();
            for (int i = 0; i < m_Contract.numItems; i++)
            {
                var contractUI = m_Contract.GetChildAt(i) as UI_ContractIcon;
                contractUI.m_state.selectedIndex = (MapInfo.Contracts != null && MapInfo.Contracts.Contains(contractUI.Contract.Id)) ? 1 : 0;
            }
        }

        void chooseContract(EventContext evt)
        {
            var contractUI = evt.data as UI_ContractIcon;
            if (MapInfo.Contracts.Contains(contractUI.Contract.Id))
            {
                MapInfo.Contracts.Remove(contractUI.Contract.Id);
                contractUI.m_state.selectedIndex = 0;
            }
            else
            {
                MapInfo.Contracts.Add(contractUI.Contract.Id);
                contractUI.m_state.selectedIndex = 1;
            }
        }
    }
}
