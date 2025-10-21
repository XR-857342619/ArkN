/** This is an automatically generated class by FairyGUI. Please do not modify it. **/

using FairyGUI;
using FairyGUI.Utils;

namespace BattleUI
{
    public partial class UI_BuffInfo : GLabel
    {
        public GTextField m_name;
        public GTextField m_type;
        public GTextField m_dataInfo;
        public GTextField m_last;
        public const string URL = "ui://vp312gabtg9gts";

        public static UI_BuffInfo CreateInstance()
        {
            return (UI_BuffInfo)UIPackage.CreateObject("BattleUI", "BuffInfo");
        }

        public override void ConstructFromXML(XML xml)
        {
            base.ConstructFromXML(xml);

            m_name = (GTextField)GetChildAt(1);
            m_type = (GTextField)GetChildAt(2);
            m_dataInfo = (GTextField)GetChildAt(3);
            m_last = (GTextField)GetChildAt(4);
            Init();
        }
        partial void Init();
    }
}