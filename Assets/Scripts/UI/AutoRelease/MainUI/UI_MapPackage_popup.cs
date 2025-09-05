/** This is an automatically generated class by FairyGUI. Please do not modify it. **/

using FairyGUI;
using FairyGUI.Utils;

namespace MainUI
{
    public partial class UI_MapPackage_popup : GComponent
    {
        public GList m_list;
        public const string URL = "ui://k4mja8t1pkaor6f";

        public static UI_MapPackage_popup CreateInstance()
        {
            return (UI_MapPackage_popup)UIPackage.CreateObject("MainUI", "MapPackage_popup");
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