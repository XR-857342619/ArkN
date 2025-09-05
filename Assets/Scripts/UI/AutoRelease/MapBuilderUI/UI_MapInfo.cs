/** This is an automatically generated class by FairyGUI. Please do not modify it. **/

using FairyGUI;
using FairyGUI.Utils;

namespace MapBuilderUI
{
    public partial class UI_MapInfo : GComponent
    {
        public Controller m_editcontract;
        public GTextInput m_SceneName;
        public GTextInput m_width;
        public GTextInput m_height;
        public GGraph m_next;
        public GTextInput m_FileName;
        public GTextInput m_MapName;
        public GTextInput m_MapDesc;
        public GTextInput m_InitHp;
        public GTextInput m_InitCost;
        public GTextInput m_BuildCount;
        public GTextInput m_MaxCost;
        public GTextInput m_BoxCount;
        public GButton m_NoBuildLimit;
        public GComboBox m_SMapPackageIndex;
        public GButton m_load;
        public GComboBox m_MapPackageIndex;
        public GComboBox m_quickLoad;
        public GList m_Contract;
        public const string URL = "ui://wof4wytzei151";

        public static UI_MapInfo CreateInstance()
        {
            return (UI_MapInfo)UIPackage.CreateObject("MapBuilderUI", "MapInfo");
        }

        public override void ConstructFromXML(XML xml)
        {
            base.ConstructFromXML(xml);

            m_editcontract = GetControllerAt(0);
            m_SceneName = (GTextInput)GetChildAt(4);
            m_width = (GTextInput)GetChildAt(7);
            m_height = (GTextInput)GetChildAt(10);
            m_next = (GGraph)GetChildAt(12);
            m_FileName = (GTextInput)GetChildAt(16);
            m_MapName = (GTextInput)GetChildAt(19);
            m_MapDesc = (GTextInput)GetChildAt(22);
            m_InitHp = (GTextInput)GetChildAt(25);
            m_InitCost = (GTextInput)GetChildAt(28);
            m_BuildCount = (GTextInput)GetChildAt(31);
            m_MaxCost = (GTextInput)GetChildAt(34);
            m_BoxCount = (GTextInput)GetChildAt(36);
            m_NoBuildLimit = (GButton)GetChildAt(39);
            m_SMapPackageIndex = (GComboBox)GetChildAt(40);
            m_load = (GButton)GetChildAt(43);
            m_MapPackageIndex = (GComboBox)GetChildAt(44);
            m_quickLoad = (GComboBox)GetChildAt(45);
            m_Contract = (GList)GetChildAt(52);
            Init();
        }
        partial void Init();
    }
}