/** This is an automatically generated class by FairyGUI. Please do not modify it. **/

using FairyGUI;
using FairyGUI.Utils;

namespace BattleUI
{
    public partial class UI_Slider2 : GSlider
    {
        public Controller m_c1;
        public GTextField m_title_2;
        public const string URL = "ui://vp312gabfji95i";

        public static UI_Slider2 CreateInstance()
        {
            return (UI_Slider2)UIPackage.CreateObject("BattleUI", "Slider2");
        }

        public override void ConstructFromXML(XML xml)
        {
            base.ConstructFromXML(xml);

            m_c1 = GetControllerAt(0);
            m_title_2 = (GTextField)GetChildAt(4);
            Init();
        }
        partial void Init();
    }
}