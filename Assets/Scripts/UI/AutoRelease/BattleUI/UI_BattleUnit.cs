/** This is an automatically generated class by FairyGUI. Please do not modify it. **/

using FairyGUI;
using FairyGUI.Utils;

namespace BattleUI
{
    public partial class UI_BattleUnit : GComponent
    {
        public Controller m_readyControl;
        public Controller m_skillCount;
        public Controller m_hpType;
        public Controller m_showBuffInfo;
        public Controller m_isPreview;
        public Controller m_spType;
        public UI_CommonBar m_hp;
        public UI_ProgressBar_boss m_bigHp;
        public GProgressBar m_bossHp;
        public UI_CommonBar m_sk;
        public UI_ProgressBar_boss m_bigSp;
        public GTextField m_skillCount_2;
        public UI_ElementBar m_elementBar;
        public GList m_progressList;
        public GButton m_showDetails;
        public GGraph m_bg;
        public GList m_infoList;
        public GTextField m_title;
        public GGroup m_BuffInfos;
        public const string URL = "ui://vp312gabh4sa41";

        public static UI_BattleUnit CreateInstance()
        {
            return (UI_BattleUnit)UIPackage.CreateObject("BattleUI", "BattleUnit");
        }

        public override void ConstructFromXML(XML xml)
        {
            base.ConstructFromXML(xml);

            m_readyControl = GetControllerAt(0);
            m_skillCount = GetControllerAt(1);
            m_hpType = GetControllerAt(2);
            m_showBuffInfo = GetControllerAt(3);
            m_isPreview = GetControllerAt(4);
            m_spType = GetControllerAt(5);
            m_hp = (UI_CommonBar)GetChildAt(0);
            m_bigHp = (UI_ProgressBar_boss)GetChildAt(1);
            m_bossHp = (GProgressBar)GetChildAt(2);
            m_sk = (UI_CommonBar)GetChildAt(3);
            m_bigSp = (UI_ProgressBar_boss)GetChildAt(4);
            m_skillCount_2 = (GTextField)GetChildAt(8);
            m_elementBar = (UI_ElementBar)GetChildAt(9);
            m_progressList = (GList)GetChildAt(10);
            m_showDetails = (GButton)GetChildAt(11);
            m_bg = (GGraph)GetChildAt(12);
            m_infoList = (GList)GetChildAt(13);
            m_title = (GTextField)GetChildAt(14);
            m_BuffInfos = (GGroup)GetChildAt(15);
            Init();
        }
        partial void Init();
    }
}