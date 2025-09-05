/** This is an automatically generated class by FairyGUI. Please do not modify it. **/

using FairyGUI;
using FairyGUI.Utils;

namespace DIY
{
    public partial class UI_AttributeLable : GLabel
    {
        public Controller m_type;
        public GButton m_bool;
        public GTextInput m_text;
        public const string URL = "ui://0tzprgeuo8qyj";

        public static UI_AttributeLable CreateInstance()
        {
            return (UI_AttributeLable)UIPackage.CreateObject("DIY", "AttributeLable");
        }

        public override void ConstructFromXML(XML xml)
        {
            base.ConstructFromXML(xml);

            m_type = GetControllerAt(0);
            m_bool = (GButton)GetChildAt(3);
            m_text = (GTextInput)GetChildAt(4);
            Init();
        }
        partial void Init();
    }
}