/** This is an automatically generated class by FairyGUI. Please do not modify it. **/

using FairyGUI;
using FairyGUI.Utils;

namespace DIY
{
    public partial class UI_AttributeLable_skill : GLabel
    {
        public Controller m_type;
        public GTextInput m_text;
        public GButton m_bool;
        public GComboBox m_combo;
        public const string URL = "ui://0tzprgeun7nor";

        public static UI_AttributeLable_skill CreateInstance()
        {
            return (UI_AttributeLable_skill)UIPackage.CreateObject("DIY", "AttributeLable_skill");
        }

        public override void ConstructFromXML(XML xml)
        {
            base.ConstructFromXML(xml);

            m_type = GetControllerAt(0);
            m_text = (GTextInput)GetChildAt(2);
            m_bool = (GButton)GetChildAt(4);
            m_combo = (GComboBox)GetChildAt(5);
            Init();
        }
        partial void Init();
    }
}