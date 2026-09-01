/** This is an automatically generated class by FairyGUI. Please do not modify it. **/

using FairyGUI;
using FairyGUI.Utils;

namespace MapBuilderUI
{
    public partial class UI_MapInfo : GComponent
    {
        public Controller m_editcontract;
        public GTextInput m_width;
        public GTextInput m_height;
        public GGraph m_next;
        public GGroup m_craetsetting;
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
        public GGroup m_basicsetting;
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
            m_width = (GTextInput)GetChildAt(4);
            m_height = (GTextInput)GetChildAt(7);
            m_next = (GGraph)GetChildAt(9);
            m_craetsetting = (GGroup)GetChildAt(11);
            m_FileName = (GTextInput)GetChildAt(14);
            m_MapName = (GTextInput)GetChildAt(17);
            m_MapDesc = (GTextInput)GetChildAt(20);
            m_InitHp = (GTextInput)GetChildAt(23);
            m_InitCost = (GTextInput)GetChildAt(26);
            m_BuildCount = (GTextInput)GetChildAt(29);
            m_MaxCost = (GTextInput)GetChildAt(32);
            m_BoxCount = (GTextInput)GetChildAt(35);
            m_NoBuildLimit = (GButton)GetChildAt(37);
            m_SMapPackageIndex = (GComboBox)GetChildAt(38);
            m_basicsetting = (GGroup)GetChildAt(40);
            m_load = (GButton)GetChildAt(42);
            m_MapPackageIndex = (GComboBox)GetChildAt(43);
            m_quickLoad = (GComboBox)GetChildAt(44);
            m_Contract = (GList)GetChildAt(51);
            Init();
        }
        partial void Init();
    }
}