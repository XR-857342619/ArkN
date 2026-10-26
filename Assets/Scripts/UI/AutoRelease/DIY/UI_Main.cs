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
        public GButton m_UnitModel;
        public GButton m_UnitAttribute;
        public GButton m_Skill;
        public GButton m_Modify;
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
        public GLoader m_unitIcon;
        public GButton m_mode;
        public GGroup m_Mode;
        public UI_AttributeLable m_modeName;
        public UI_AttributeLable m_unitName;
        public GButton m_AnimationLoop;
        public GButton m_PlayBtn;
        public GButton m_PauseBtn;
        public GList m_AnimationList;
        public GGraph m_spineMode;
        public UI_AttributeLable m_HP;
        public UI_AttributeLable m_Def;
        public UI_AttributeLable m_MicDef;
        public UI_AttributeLable m_Atk;
        public GGroup m_uintModleSetting;
        public GList m_addUnitAttribute;
        public GComboBox m_selectUnitAttribute;
        public GButton m_newUnitAttribute;
        public GButton m_delUnitAttribute;
        public GComboBox m_selectAttributeGroup;
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
        public GButton m_back_2;
        public GList m_blocks;
        public GGroup m_editRange;
        public GComboBox m_jsonFiles;
        public GComboBox m_folders;
        public GButton m_save;
        public GButton m_saveAsNew;
        public GGroup m_selectFile;
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
            m_UnitModel = (GButton)GetChildAt(5);
            m_UnitAttribute = (GButton)GetChildAt(6);
            m_Skill = (GButton)GetChildAt(7);
            m_Modify = (GButton)GetChildAt(8);
            m_switchBtns = (GGroup)GetChildAt(9);
            m_selectUnitCombobox = (GComboBox)GetChildAt(10);
            m_isNewBtn = (GButton)GetChildAt(11);
            m_normal = (GButton)GetChildAt(12);
            m_opreator = (GButton)GetChildAt(13);
            m_enemy = (GButton)GetChildAt(14);
            m_unitNameInput = (GTextInput)GetChildAt(16);
            m_selecUnit = (GGroup)GetChildAt(18);
            m_OpAtkGap = (UI_AttributeLable)GetChildAt(19);
            m_Cost = (UI_AttributeLable)GetChildAt(20);
            m_OpStopCount = (UI_AttributeLable)GetChildAt(21);
            m_ResetTime = (UI_AttributeLable)GetChildAt(22);
            m_Opreator = (GGroup)GetChildAt(23);
            m_Weight = (UI_AttributeLable)GetChildAt(24);
            m_EnAtkGap = (UI_AttributeLable)GetChildAt(25);
            m_EnStopCount = (UI_AttributeLable)GetChildAt(26);
            m_Enmey = (GGroup)GetChildAt(27);
            m_NotUseTile = (UI_AttributeLable)GetChildAt(28);
            m_NormalUnit = (GGroup)GetChildAt(29);
            m_unitIcon = (GLoader)GetChildAt(30);
            m_mode = (GButton)GetChildAt(31);
            m_Mode = (GGroup)GetChildAt(32);
            m_modeName = (UI_AttributeLable)GetChildAt(33);
            m_unitName = (UI_AttributeLable)GetChildAt(34);
            m_AnimationLoop = (GButton)GetChildAt(35);
            m_PlayBtn = (GButton)GetChildAt(36);
            m_PauseBtn = (GButton)GetChildAt(37);
            m_AnimationList = (GList)GetChildAt(38);
            m_spineMode = (GGraph)GetChildAt(39);
            m_HP = (UI_AttributeLable)GetChildAt(40);
            m_Def = (UI_AttributeLable)GetChildAt(41);
            m_MicDef = (UI_AttributeLable)GetChildAt(42);
            m_Atk = (UI_AttributeLable)GetChildAt(43);
            m_uintModleSetting = (GGroup)GetChildAt(45);
            m_addUnitAttribute = (GList)GetChildAt(46);
            m_selectUnitAttribute = (GComboBox)GetChildAt(47);
            m_newUnitAttribute = (GButton)GetChildAt(48);
            m_delUnitAttribute = (GButton)GetChildAt(49);
            m_selectAttributeGroup = (GComboBox)GetChildAt(50);
            m_unitAttributeList = (GGroup)GetChildAt(52);
            m_unit = (GGroup)GetChildAt(53);
            m_skillList = (GList)GetChildAt(54);
            m_newSkill = (GButton)GetChildAt(55);
            m_delSkill = (GButton)GetChildAt(56);
            m_selectSkillAttribute = (GComboBox)GetChildAt(57);
            m_newSkillAttribute = (GButton)GetChildAt(58);
            m_addSkillAttribute = (GList)GetChildAt(59);
            m_delSkillAttribute = (GButton)GetChildAt(60);
            m_skillAttributeList = (GGroup)GetChildAt(61);
            m_skill = (GGroup)GetChildAt(62);
            m_back = (GButton)GetChildAt(63);
            m_icons = (GList)GetChildAt(65);
            m_iconList = (GGroup)GetChildAt(66);
            m_tip = (GRichTextField)GetChildAt(67);
            m_back_2 = (GButton)GetChildAt(68);
            m_blocks = (GList)GetChildAt(70);
            m_editRange = (GGroup)GetChildAt(71);
            m_jsonFiles = (GComboBox)GetChildAt(72);
            m_folders = (GComboBox)GetChildAt(73);
            m_save = (GButton)GetChildAt(74);
            m_saveAsNew = (GButton)GetChildAt(75);
            m_selectFile = (GGroup)GetChildAt(76);
            Init();
        }
        partial void Init();
    }
}