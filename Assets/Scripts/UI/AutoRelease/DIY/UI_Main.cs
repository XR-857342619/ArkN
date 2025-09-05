/** This is an automatically generated class by FairyGUI. Please do not modify it. **/

using FairyGUI;
using FairyGUI.Utils;

namespace DIY
{
    public partial class UI_Main : GComponent
    {
        public Controller m_funPage;
        public Controller m_isNew;
        public Controller m_unitType;
        public Controller m_selectIcon;
        public GGroup m_background;
        public GButton m_exit;
        public GButton m_Unit;
        public GButton m_Skill;
        public GButton m_Buff;
        public GButton m_Modify;
        public GComboBox m_excels;
        public GComboBox m_folders;
        public GGroup m_switchBtns;
        public GComboBox m_selectUnitCombobox;
        public GButton m_isNewBtn;
        public GButton m_normal;
        public GButton m_opreator;
        public GButton m_enemy;
        public GTextInput m_unitNameInput;
        public GGroup m_selecUnit;
        public UI_AttributeLable m_OpAtkGap;
        public UI_AttributeLable m_Cost;
        public UI_AttributeLable m_OpStopCount;
        public UI_AttributeLable m_ResetTime;
        public GGroup m_Opreator;
        public UI_AttributeLable m_Weight;
        public UI_AttributeLable m_EnAtkGap;
        public UI_AttributeLable m_EnStopCount;
        public GGroup m_Enmey;
        public UI_AttributeLable m_NotUseTile;
        public GGroup m_NormalUnit;
        public UI_AttributeLable m_modeName;
        public UI_AttributeLable m_unitName;
        public UI_AttributeLable m_HP;
        public UI_AttributeLable m_Def;
        public UI_AttributeLable m_MicDef;
        public UI_AttributeLable m_Atk;
        public GGroup m_uintGeneralAttribute;
        public GLoader m_unitIcon;
        public GButton m_mode;
        public GGroup m_Mode;
        public GComboBox m_selectUnitAttribute;
        public GButton m_newUnitAttribute;
        public GList m_addUnitAttribute;
        public GButton m_delUnitAttribute;
        public GGroup m_unitAttributeList;
        public GGroup m_unit;
        public GList m_skillList;
        public GButton m_newSkill;
        public GButton m_delSkill;
        public GComboBox m_selectSkillAttribute;
        public GButton m_newSkillAttribute;
        public GList m_addSkillAttribute;
        public GButton m_delSkillAttribute;
        public GGroup m_skillAttributeList;
        public GGroup m_skill;
        public GButton m_back;
        public GList m_icons;
        public GGroup m_iconList;
        public GRichTextField m_tip;
        public GButton m_save;
        public GButton m_saveAsNew;
        public GButton m_back_2;
        public GList m_blocks;
        public GGroup m_editRange;
        public const string URL = "ui://0tzprgeut03z0";

        public static UI_Main CreateInstance()
        {
            return (UI_Main)UIPackage.CreateObject("DIY", "Main");
        }

        public override void ConstructFromXML(XML xml)
        {
            base.ConstructFromXML(xml);

            m_funPage = GetControllerAt(0);
            m_isNew = GetControllerAt(1);
            m_unitType = GetControllerAt(2);
            m_selectIcon = GetControllerAt(3);
            m_background = (GGroup)GetChildAt(3);
            m_exit = (GButton)GetChildAt(4);
            m_Unit = (GButton)GetChildAt(5);
            m_Skill = (GButton)GetChildAt(6);
            m_Buff = (GButton)GetChildAt(7);
            m_Modify = (GButton)GetChildAt(8);
            m_excels = (GComboBox)GetChildAt(9);
            m_folders = (GComboBox)GetChildAt(10);
            m_switchBtns = (GGroup)GetChildAt(11);
            m_selectUnitCombobox = (GComboBox)GetChildAt(12);
            m_isNewBtn = (GButton)GetChildAt(13);
            m_normal = (GButton)GetChildAt(14);
            m_opreator = (GButton)GetChildAt(15);
            m_enemy = (GButton)GetChildAt(16);
            m_unitNameInput = (GTextInput)GetChildAt(18);
            m_selecUnit = (GGroup)GetChildAt(20);
            m_OpAtkGap = (UI_AttributeLable)GetChildAt(21);
            m_Cost = (UI_AttributeLable)GetChildAt(22);
            m_OpStopCount = (UI_AttributeLable)GetChildAt(23);
            m_ResetTime = (UI_AttributeLable)GetChildAt(24);
            m_Opreator = (GGroup)GetChildAt(25);
            m_Weight = (UI_AttributeLable)GetChildAt(26);
            m_EnAtkGap = (UI_AttributeLable)GetChildAt(27);
            m_EnStopCount = (UI_AttributeLable)GetChildAt(28);
            m_Enmey = (GGroup)GetChildAt(29);
            m_NotUseTile = (UI_AttributeLable)GetChildAt(30);
            m_NormalUnit = (GGroup)GetChildAt(31);
            m_modeName = (UI_AttributeLable)GetChildAt(32);
            m_unitName = (UI_AttributeLable)GetChildAt(33);
            m_HP = (UI_AttributeLable)GetChildAt(34);
            m_Def = (UI_AttributeLable)GetChildAt(35);
            m_MicDef = (UI_AttributeLable)GetChildAt(36);
            m_Atk = (UI_AttributeLable)GetChildAt(37);
            m_uintGeneralAttribute = (GGroup)GetChildAt(38);
            m_unitIcon = (GLoader)GetChildAt(39);
            m_mode = (GButton)GetChildAt(40);
            m_Mode = (GGroup)GetChildAt(41);
            m_selectUnitAttribute = (GComboBox)GetChildAt(42);
            m_newUnitAttribute = (GButton)GetChildAt(43);
            m_addUnitAttribute = (GList)GetChildAt(44);
            m_delUnitAttribute = (GButton)GetChildAt(45);
            m_unitAttributeList = (GGroup)GetChildAt(46);
            m_unit = (GGroup)GetChildAt(47);
            m_skillList = (GList)GetChildAt(48);
            m_newSkill = (GButton)GetChildAt(49);
            m_delSkill = (GButton)GetChildAt(50);
            m_selectSkillAttribute = (GComboBox)GetChildAt(51);
            m_newSkillAttribute = (GButton)GetChildAt(52);
            m_addSkillAttribute = (GList)GetChildAt(53);
            m_delSkillAttribute = (GButton)GetChildAt(54);
            m_skillAttributeList = (GGroup)GetChildAt(55);
            m_skill = (GGroup)GetChildAt(56);
            m_back = (GButton)GetChildAt(57);
            m_icons = (GList)GetChildAt(59);
            m_iconList = (GGroup)GetChildAt(60);
            m_tip = (GRichTextField)GetChildAt(61);
            m_save = (GButton)GetChildAt(62);
            m_saveAsNew = (GButton)GetChildAt(63);
            m_back_2 = (GButton)GetChildAt(64);
            m_blocks = (GList)GetChildAt(66);
            m_editRange = (GGroup)GetChildAt(67);
            Init();
        }
        partial void Init();
    }
}