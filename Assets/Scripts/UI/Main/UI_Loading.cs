using FairyGUI;
using UnityEngine;

namespace MainUI
{
    public partial class UI_Loading
    {
        /// <summary>
        /// 设置加载进度条，progress 范围 0~1。
        /// </summary>
        public void SetProgress(float progress)
        {
            if (m_loadingBar == null) return;

            float value = Mathf.Clamp01(progress);
            m_loadingBar.max = 1f;
            m_loadingBar.value = value;
        }

        /// <summary>
        /// 设置当前加载状态描述文本。
        /// </summary>
        public void SetLoadDes(string des)
        {
            if (m_loadDes == null) return;

            m_loadDes.text = des;
        }

        /// <summary>
        /// 同时设置进度和描述。
        /// </summary>
        public void SetProgress(float progress, string des)
        {
            SetProgress(progress);
            SetLoadDes(des);
        }
    }
}