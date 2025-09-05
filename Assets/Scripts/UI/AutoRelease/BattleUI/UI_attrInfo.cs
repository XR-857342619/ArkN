/** This is an automatically generated class by FairyGUI. Please do not modify it. **/

using FairyGUI;
using FairyGUI.Utils;

namespace BattleUI
{
    public partial class UI_attrInfo : GLabel
    {
        public GTextField m_value;
        public const string URL = "ui://vp312gabbcc35y";

        public static UI_attrInfo CreateInstance()
        {
            return (UI_attrInfo)UIPackage.CreateObject("BattleUI", "attrInfo");
        }

        public override void ConstructFromXML(XML xml)
        {
            base.ConstructFromXML(xml);

            m_value = (GTextField)GetChildAt(1);
            Init();
        }
        partial void Init();
    }
}