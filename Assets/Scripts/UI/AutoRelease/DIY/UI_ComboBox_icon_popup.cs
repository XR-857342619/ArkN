/** This is an automatically generated class by FairyGUI. Please do not modify it. **/

using FairyGUI;
using FairyGUI.Utils;

namespace DIY
{
    public partial class UI_ComboBox_icon_popup : GComponent
    {
        public GList m_list;
        public const string URL = "ui://0tzprgeuo8qyh";

        public static UI_ComboBox_icon_popup CreateInstance()
        {
            return (UI_ComboBox_icon_popup)UIPackage.CreateObject("DIY", "ComboBox_icon_popup");
        }

        public override void ConstructFromXML(XML xml)
        {
            base.ConstructFromXML(xml);

            m_list = (GList)GetChildAt(1);
            Init();
        }
        partial void Init();
    }
}