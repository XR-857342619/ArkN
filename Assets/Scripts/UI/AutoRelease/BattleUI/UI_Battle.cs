/** This is an automatically generated class by FairyGUI. Please do not modify it. **/

using FairyGUI;
using FairyGUI.Utils;

namespace BattleUI
{
    public partial class UI_Battle : GComponent
    {
        public Controller m_state;
        public Controller m_win;
        public Controller m_isPreview;
        public Controller m_showUnitList;
        public GTextField m_enemy;
        public GTextField m_hp;
        public GComponent m_Units;
        public GComponent m_DamageInfo;
        public GComponent m_SkillUseBack;
        public UI_BattleLeft m_left;
        public GComponent m_Builds;
        public UI_SkillUsePanel m_SkillUsePanel;
        public GComponent m_Setting;
        public GTextField m_number;
        public GTextField m_cost;
        public UI_CostBar m_costBar;
        public GGroup m_youxia;
        public UI_FastSpeed m_GameSpeed;
        public UI_Pause m_Pause;
        public GGroup m_normalGroup;
        public GList m_UnitList;
        public GGraph m_background;
        public GGraph m_bg;
        public UI_Slider2 m_gameSpeed;
        public GButton m_isInfCost;
        public GButton m_isInfHealth;
        public GButton m_isNoCD;
        public GButton m_isInfUnitCount;
        public GButton m_isNoLimitBuild;
        public UI_GiveUP m_GiveUp2;
        public UI_Slider2 m_skillPowerSpeed;
        public GButton m_exitSetting;
        public GGroup m_previewGroup;
        public GComponent m_endClick;
        public GLoader m_endPic;
        public GTextField m_result;
        public GImage m_w1;
        public GImage m_w2;
        public GImage m_w3;
        public GGroup m_endGroup;
        public GList m_DamageInfoList;
        public GGraph m_GiveUpBack;
        public GComponent m_CancelGiveUp;
        public UI_GiveUP m_GiveUp;
        public GGroup m_giveupGroup;
        public Transition m_win1;
        public Transition m_win2;
        public Transition m_win3;
        public Transition m_reset;
        public const string URL = "ui://vp312gabf1460";

        public static UI_Battle CreateInstance()
        {
            return (UI_Battle)UIPackage.CreateObject("BattleUI", "Battle");
        }

        public override void ConstructFromXML(XML xml)
        {
            base.ConstructFromXML(xml);

            m_state = GetControllerAt(0);
            m_win = GetControllerAt(1);
            m_isPreview = GetControllerAt(2);
            m_showUnitList = GetControllerAt(3);
            m_enemy = (GTextField)GetChildAt(1);
            m_hp = (GTextField)GetChildAt(2);
            m_Units = (GComponent)GetChildAt(3);
            m_DamageInfo = (GComponent)GetChildAt(4);
            m_SkillUseBack = (GComponent)GetChildAt(5);
            m_left = (UI_BattleLeft)GetChildAt(6);
            m_Builds = (GComponent)GetChildAt(7);
            m_SkillUsePanel = (UI_SkillUsePanel)GetChildAt(8);
            m_Setting = (GComponent)GetChildAt(9);
            m_number = (GTextField)GetChildAt(11);
            m_cost = (GTextField)GetChildAt(12);
            m_costBar = (UI_CostBar)GetChildAt(13);
            m_youxia = (GGroup)GetChildAt(14);
            m_GameSpeed = (UI_FastSpeed)GetChildAt(15);
            m_Pause = (UI_Pause)GetChildAt(16);
            m_normalGroup = (GGroup)GetChildAt(18);
            m_UnitList = (GList)GetChildAt(19);
            m_background = (GGraph)GetChildAt(20);
            m_bg = (GGraph)GetChildAt(21);
            m_gameSpeed = (UI_Slider2)GetChildAt(26);
            m_isInfCost = (GButton)GetChildAt(27);
            m_isInfHealth = (GButton)GetChildAt(28);
            m_isNoCD = (GButton)GetChildAt(30);
            m_isInfUnitCount = (GButton)GetChildAt(33);
            m_isNoLimitBuild = (GButton)GetChildAt(34);
            m_GiveUp2 = (UI_GiveUP)GetChildAt(36);
            m_skillPowerSpeed = (UI_Slider2)GetChildAt(37);
            m_exitSetting = (GButton)GetChildAt(38);
            m_previewGroup = (GGroup)GetChildAt(39);
            m_endClick = (GComponent)GetChildAt(40);
            m_endPic = (GLoader)GetChildAt(41);
            m_result = (GTextField)GetChildAt(45);
            m_w1 = (GImage)GetChildAt(46);
            m_w2 = (GImage)GetChildAt(47);
            m_w3 = (GImage)GetChildAt(48);
            m_endGroup = (GGroup)GetChildAt(51);
            m_DamageInfoList = (GList)GetChildAt(52);
            m_GiveUpBack = (GGraph)GetChildAt(53);
            m_CancelGiveUp = (GComponent)GetChildAt(56);
            m_GiveUp = (UI_GiveUP)GetChildAt(57);
            m_giveupGroup = (GGroup)GetChildAt(58);
            m_win1 = GetTransitionAt(0);
            m_win2 = GetTransitionAt(1);
            m_win3 = GetTransitionAt(2);
            m_reset = GetTransitionAt(3);
            Init();
        }
        partial void Init();
    }
}