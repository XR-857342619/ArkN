/** This is an automatically generated class by FairyGUI. Please do not modify it. **/

using FairyGUI;
using FairyGUI.Utils;

namespace MainUI
{
    public partial class UI_ExcelItem : GLabel
    {
        public Controller m_leaf;
        public Controller m_expanded;
        public Controller m_selectedIndex;
        public GButton m_selectBtn;
        public GGraph m_indent;
        public GTextField m_path;
        public const string URL = "ui://k4mja8t16ew9r6q";

        public static UI_ExcelItem CreateInstance()
        {
            return (UI_ExcelItem)UIPackage.CreateObject("MainUI", "ExcelItem");
        }

        public override void ConstructFromXML(XML xml)
        {
            base.ConstructFromXML(xml);

            m_leaf = GetControllerAt(0);
            m_expanded = GetControllerAt(1);
            m_selectedIndex = GetControllerAt(2);
            m_selectBtn = (GButton)GetChildAt(3);
            m_indent = (GGraph)GetChildAt(4);
            m_path = (GTextField)GetChildAt(7);
            Init();
        }
        partial void Init();
    }
}