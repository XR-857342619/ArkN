/** This is an automatically generated class by FairyGUI. Please do not modify it. **/

using FairyGUI;
using FairyGUI.Utils;

namespace MapBuilderUI
{
    public partial class UI_MapInfo : GComponent
    {
        public Controller m_editcontract;
        public GComboBox m_SelectBGM;
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
            m_SelectBGM = (GComboBox)GetChildAt(1);
            m_width = (GTextInput)GetChildAt(5);
            m_height = (GTextInput)GetChildAt(8);
            m_next = (GGraph)GetChildAt(10);
            m_craetsetting = (GGroup)GetChildAt(12);
            m_FileName = (GTextInput)GetChildAt(15);
            m_MapName = (GTextInput)GetChildAt(18);
            m_MapDesc = (GTextInput)GetChildAt(21);
            m_InitHp = (GTextInput)GetChildAt(24);
            m_InitCost = (GTextInput)GetChildAt(27);
            m_BuildCount = (GTextInput)GetChildAt(30);
            m_MaxCost = (GTextInput)GetChildAt(33);
            m_BoxCount = (GTextInput)GetChildAt(36);
            m_NoBuildLimit = (GButton)GetChildAt(38);
            m_SMapPackageIndex = (GComboBox)GetChildAt(39);
            m_basicsetting = (GGroup)GetChildAt(41);
            m_load = (GButton)GetChildAt(43);
            m_MapPackageIndex = (GComboBox)GetChildAt(44);
            m_quickLoad = (GComboBox)GetChildAt(45);
            m_Contract = (GList)GetChildAt(52);
            Init();
        }
        partial void Init();
    }
}