/** This is an automatically generated class by FairyGUI. Please do not modify it. **/

using FairyGUI;
using FairyGUI.Utils;

namespace MainUI
{
    public partial class UI_Battle : GComponent
    {
        public Controller m_showLevelInfo;
        public Controller m_contractChoose;
        public Controller m_ShowMain;
        public Controller m_PackageEdit;
        public Controller m_mode;
        public UI_World1 m_world;
        public GButton m_back;
        public UI_LevelInfo m_levelInfo;
        public GGraph m_contractBack;
        public GList m_contracts;
        public GComboBox m_MapPackage;
        public GTextField m_tip;
        public GButton m_EditExcel;
        public GButton m_MapLink;
        public GButton m_MapMove;
        public GButton m_DelLink;
        public GButton m_Editcomfirm;
        public GButton m_Linkcomfirm;
        public GButton m_Excelcomfirm;
        public GButton m_Movecomfirm;
        public GButton m_EditorMod;
        public GButton m_EditorMod_1;
        public GTree m_ExcelList;
        public const string URL = "ui://k4mja8t1kbtew";

        public static UI_Battle CreateInstance()
        {
            return (UI_Battle)UIPackage.CreateObject("MainUI", "Battle");
        }

        public override void ConstructFromXML(XML xml)
        {
            base.ConstructFromXML(xml);

            m_showLevelInfo = GetControllerAt(0);
            m_contractChoose = GetControllerAt(1);
            m_ShowMain = GetControllerAt(2);
            m_PackageEdit = GetControllerAt(3);
            m_mode = GetControllerAt(4);
            m_world = (UI_World1)GetChildAt(1);
            m_back = (GButton)GetChildAt(2);
            m_levelInfo = (UI_LevelInfo)GetChildAt(3);
            m_contractBack = (GGraph)GetChildAt(4);
            m_contracts = (GList)GetChildAt(6);
            m_MapPackage = (GComboBox)GetChildAt(7);
            m_tip = (GTextField)GetChildAt(9);
            m_EditExcel = (GButton)GetChildAt(10);
            m_MapLink = (GButton)GetChildAt(11);
            m_MapMove = (GButton)GetChildAt(12);
            m_DelLink = (GButton)GetChildAt(13);
            m_Editcomfirm = (GButton)GetChildAt(14);
            m_Linkcomfirm = (GButton)GetChildAt(15);
            m_Excelcomfirm = (GButton)GetChildAt(16);
            m_Movecomfirm = (GButton)GetChildAt(17);
            m_EditorMod = (GButton)GetChildAt(18);
            m_EditorMod_1 = (GButton)GetChildAt(19);
            m_ExcelList = (GTree)GetChildAt(20);
            Init();
        }
        partial void Init();
    }
}