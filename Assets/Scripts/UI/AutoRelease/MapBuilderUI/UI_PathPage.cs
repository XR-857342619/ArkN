/** This is an automatically generated class by FairyGUI. Please do not modify it. **/

using FairyGUI;
using FairyGUI.Utils;

namespace MapBuilderUI
{
    public partial class UI_PathPage : GComponent
    {
        public Controller m_select;
        public Controller m_ShowPathList;
        public Controller m_ShowPathInfo;
        public GList m_Paths;
        public GButton m_AddPath;
        public GButton m_DeletePath;
        public GList m_PathPoints;
        public GButton m_AddPoint;
        public GButton m_DeletePoint;
        public GButton m_CopyPath;
        public GButton m_InsertPoint;
        public GButton m_PathListBtn_0;
        public GButton m_PathListBtn_1;
        public GButton m_PathInfoBtn_0;
        public GButton m_PathInfoBtn_1;
        public const string URL = "ui://wof4wytzei158";

        public static UI_PathPage CreateInstance()
        {
            return (UI_PathPage)UIPackage.CreateObject("MapBuilderUI", "PathPage");
        }

        public override void ConstructFromXML(XML xml)
        {
            base.ConstructFromXML(xml);

            m_select = GetControllerAt(0);
            m_ShowPathList = GetControllerAt(1);
            m_ShowPathInfo = GetControllerAt(2);
            m_Paths = (GList)GetChildAt(1);
            m_AddPath = (GButton)GetChildAt(2);
            m_DeletePath = (GButton)GetChildAt(3);
            m_PathPoints = (GList)GetChildAt(5);
            m_AddPoint = (GButton)GetChildAt(6);
            m_DeletePoint = (GButton)GetChildAt(7);
            m_CopyPath = (GButton)GetChildAt(9);
            m_InsertPoint = (GButton)GetChildAt(10);
            m_PathListBtn_0 = (GButton)GetChildAt(11);
            m_PathListBtn_1 = (GButton)GetChildAt(12);
            m_PathInfoBtn_0 = (GButton)GetChildAt(13);
            m_PathInfoBtn_1 = (GButton)GetChildAt(14);
            Init();
        }
        partial void Init();
    }
}