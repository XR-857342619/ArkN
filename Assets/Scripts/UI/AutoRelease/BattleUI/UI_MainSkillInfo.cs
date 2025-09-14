/** This is an automatically generated class by FairyGUI. Please do not modify it. **/

using FairyGUI;
using FairyGUI.Utils;

namespace BattleUI
{
    public partial class UI_MainSkillInfo : GProgressBar
    {
        public Controller m_using;
        public Controller m_canStop;
        public Controller m_isReady;
        public Controller m_isInfinity;
        public GLoader m_icon;
        public GTextField m_IsInfinity;
        public const string URL = "ui://vp312gabkbte48";

        public static UI_MainSkillInfo CreateInstance()
        {
            return (UI_MainSkillInfo)UIPackage.CreateObject("BattleUI", "MainSkillInfo");
        }

        public override void ConstructFromXML(XML xml)
        {
            base.ConstructFromXML(xml);

            m_using = GetControllerAt(0);
            m_canStop = GetControllerAt(1);
            m_isReady = GetControllerAt(2);
            m_isInfinity = GetControllerAt(3);
            m_icon = (GLoader)GetChildAt(1);
            m_IsInfinity = (GTextField)GetChildAt(9);
            Init();
        }
        partial void Init();
    }
}