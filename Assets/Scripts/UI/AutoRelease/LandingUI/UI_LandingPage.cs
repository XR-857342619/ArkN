/** This is an automatically generated class by FairyGUI. Please do not modify it. **/

using FairyGUI;
using FairyGUI.Utils;

namespace LandingUI
{
    public partial class UI_LandingPage : GComponent
    {
        public Controller m_IsLanded;
        public GProgressBar m_landingProgress;
        public GTextField m_des;
        public const string URL = "ui://tpz9wgqfjdpn0";

        public static UI_LandingPage CreateInstance()
        {
            return (UI_LandingPage)UIPackage.CreateObject("LandingUI", "LandingPage");
        }

        public override void ConstructFromXML(XML xml)
        {
            base.ConstructFromXML(xml);

            m_IsLanded = GetControllerAt(0);
            m_landingProgress = (GProgressBar)GetChildAt(2);
            m_des = (GTextField)GetChildAt(3);
            Init();
        }
        partial void Init();
    }
}