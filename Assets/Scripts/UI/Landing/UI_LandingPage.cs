using FairyGUI;
using FairyGUI.Utils;

namespace LandingUI
{
    public partial class UI_LandingPage : IGameUIView
    {
        public void Enter()
        {
            // Code to execute when entering the landing page
        }

        /// <summary>
        /// 更新加载进度与描述文本。
        /// </summary>
        public void SetProgress(float progress, string desc)
        {
            if (m_landingProgress != null)
                m_landingProgress.value = m_landingProgress.max * progress;
            if (m_des != null)
                m_des.text = desc;
        }

        /// <summary>
        /// 全部加载完成后调用，切换控制器使加载页淡出。
        /// </summary>
        public void Complete()
        {
            if (m_IsLanded != null)
                m_IsLanded.selectedIndex = 1;
        }
    }
}