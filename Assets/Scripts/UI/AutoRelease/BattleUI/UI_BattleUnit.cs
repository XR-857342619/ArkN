/** This is an automatically generated class by FairyGUI. Please do not modify it. **/

using FairyGUI;
using FairyGUI.Utils;

namespace BattleUI
{
    public partial class UI_BattleUnit : GComponent
    {
        public Controller m_readyControl;
        public Controller m_skillCount;
        public Controller m_unitType;
        public Controller m_showBuffInfo;
        public Controller m_isPreview;
        public GProgressBar m_hp;
        public UI_SK0 m_sk;
        public GTextField m_skillCount_2;
        public UI_Hp2 m_eHp;
        public UI_ElementBar m_elementBar;
        public GList m_progressList;
        public GProgressBar m_bigHp;
        public GProgressBar m_bossHp;
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
            m_unitType = GetControllerAt(2);
            m_showBuffInfo = GetControllerAt(3);
            m_isPreview = GetControllerAt(4);
            m_hp = (GProgressBar)GetChildAt(0);
            m_sk = (UI_SK0)GetChildAt(1);
            m_skillCount_2 = (GTextField)GetChildAt(5);
            m_eHp = (UI_Hp2)GetChildAt(6);
            m_elementBar = (UI_ElementBar)GetChildAt(7);
            m_progressList = (GList)GetChildAt(8);
            m_bigHp = (GProgressBar)GetChildAt(9);
            m_bossHp = (GProgressBar)GetChildAt(10);
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