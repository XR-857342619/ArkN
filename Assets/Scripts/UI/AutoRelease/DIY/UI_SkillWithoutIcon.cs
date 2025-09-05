/** This is an automatically generated class by FairyGUI. Please do not modify it. **/

using FairyGUI;
using FairyGUI.Utils;

namespace DIY
{
    public partial class UI_SkillWithoutIcon : GButton
    {
        public GTextField m_describe;
        public GComboBox m_type;
        public UI_AttributeLable_skill m_readyType;
        public UI_AttributeLable_skill m_useType;
        public UI_AttributeLable_skill m_trigger;
        public UI_AttributeLable_skill m_attackFly;
        public UI_AttributeLable_skill m_damageType;
        public UI_AttributeLable_skill m_targetTeam;
        public const string URL = "ui://0tzprgeuo8qyn";

        public static UI_SkillWithoutIcon CreateInstance()
        {
            return (UI_SkillWithoutIcon)UIPackage.CreateObject("DIY", "SkillWithoutIcon");
        }

        public override void ConstructFromXML(XML xml)
        {
            base.ConstructFromXML(xml);

            m_describe = (GTextField)GetChildAt(4);
            m_type = (GComboBox)GetChildAt(6);
            m_readyType = (UI_AttributeLable_skill)GetChildAt(7);
            m_useType = (UI_AttributeLable_skill)GetChildAt(8);
            m_trigger = (UI_AttributeLable_skill)GetChildAt(9);
            m_attackFly = (UI_AttributeLable_skill)GetChildAt(10);
            m_damageType = (UI_AttributeLable_skill)GetChildAt(11);
            m_targetTeam = (UI_AttributeLable_skill)GetChildAt(12);
            Init();
        }
        partial void Init();
    }
}