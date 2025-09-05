/** This is an automatically generated class by FairyGUI. Please do not modify it. **/

using FairyGUI;
using FairyGUI.Utils;

namespace MainUI
{
    public partial class UI_Main : GComponent
    {
        public Controller m_settingC;
        public Controller m_subSetting;
        public GLoader m_standPic;
        public GTextInput m_Name;
        public GButton m_Setting;
        public GTextField m_Version;
        public GButton m_Export;
        public GButton m_rogue;
        public GButton m_member;
        public GButton m_battle;
        public GButton m_Map;
        public GButton m_team;
        public GButton m_close;
        public GButton m_importSpine;
        public GSlider m_bgm;
        public GGroup m_bgmG;
        public GButton m_ShowHp;
        public GButton m_ShowElement;
        public GGroup m_gamesteeing;
        public GTree m_ExcelList;
        public GGroup m_ExcelSetting;
        public GGroup m_subpage;
        public const string URL = "ui://k4mja8t1kbte0";

        public static UI_Main CreateInstance()
        {
            return (UI_Main)UIPackage.CreateObject("MainUI", "Main");
        }

        public override void ConstructFromXML(XML xml)
        {
            base.ConstructFromXML(xml);

            m_settingC = GetControllerAt(0);
            m_subSetting = GetControllerAt(1);
            m_standPic = (GLoader)GetChildAt(1);
            m_Name = (GTextInput)GetChildAt(3);
            m_Setting = (GButton)GetChildAt(4);
            m_Version = (GTextField)GetChildAt(6);
            m_Export = (GButton)GetChildAt(7);
            m_rogue = (GButton)GetChildAt(8);
            m_member = (GButton)GetChildAt(9);
            m_battle = (GButton)GetChildAt(10);
            m_Map = (GButton)GetChildAt(11);
            m_team = (GButton)GetChildAt(12);
            m_close = (GButton)GetChildAt(17);
            m_importSpine = (GButton)GetChildAt(23);
            m_bgm = (GSlider)GetChildAt(24);
            m_bgmG = (GGroup)GetChildAt(26);
            m_ShowHp = (GButton)GetChildAt(28);
            m_ShowElement = (GButton)GetChildAt(29);
            m_gamesteeing = (GGroup)GetChildAt(31);
            m_ExcelList = (GTree)GetChildAt(33);
            m_ExcelSetting = (GGroup)GetChildAt(34);
            m_subpage = (GGroup)GetChildAt(35);
            Init();
        }
        partial void Init();
    }
}