using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MainUI
{
    partial class UI_LevelInfo
    {
        public void SetInfo(string packageName, string mapId)
        {
            m_id.text = mapId;
            var mapData = Database.Instance.GetMap(packageName, mapId);
            if (mapData == null)
            {
                // 地图存档缺失/未加载：显示提示而不是抛 NRE 冻结界面
                m_name.text = "（地图数据缺失）";
                m_Desc.text = $"未找到 {packageName}/{mapId} 的存档数据，请确认地图已在编辑器中保存。";
                return;
            }
            m_name.text = mapData.MapName;
            m_Desc.text = mapData.Description;
        }
    }
}
