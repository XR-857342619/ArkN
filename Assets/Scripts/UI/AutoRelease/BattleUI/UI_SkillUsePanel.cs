/** This is an automatically generated class by FairyGUI. Please do not modify it. **/

using FairyGUI;
using FairyGUI.Utils;

namespace BattleUI
{
    public partial class UI_SkillUsePanel : GComponent
    {
        public UI_MainSkillInfo m_mainSkillInfo;
        public GComponent m_Leave;
        public GButton m_ShowSkillRange;
        public const string URL = "ui://vp312gabkbte47";

        public static UI_SkillUsePanel CreateInstance()
        {
            return (UI_SkillUsePanel)UIPackage.CreateObject("BattleUI", "SkillUsePanel");
        }

        public override void ConstructFromXML(XML xml)
        {
            base.ConstructFromXML(xml);

            m_mainSkillInfo = (UI_MainSkillInfo)GetChildAt(2);
            m_Leave = (GComponent)GetChildAt(3);
            m_ShowSkillRange = (GButton)GetChildAt(4);
            Init();
        }
        partial void Init();
    }
}