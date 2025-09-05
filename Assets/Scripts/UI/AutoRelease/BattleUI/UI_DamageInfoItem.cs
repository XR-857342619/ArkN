/** This is an automatically generated class by FairyGUI. Please do not modify it. **/

using FairyGUI;
using FairyGUI.Utils;

namespace BattleUI
{
    public partial class UI_DamageInfoItem : GLabel
    {
        public GSlider m_nomal;
        public GSlider m_real;
        public GSlider m_magic;
        public GSlider m_total;
        public const string URL = "ui://vp312gabfji95m";

        public static UI_DamageInfoItem CreateInstance()
        {
            return (UI_DamageInfoItem)UIPackage.CreateObject("BattleUI", "DamageInfoItem");
        }

        public override void ConstructFromXML(XML xml)
        {
            base.ConstructFromXML(xml);

            m_nomal = (GSlider)GetChildAt(2);
            m_real = (GSlider)GetChildAt(3);
            m_magic = (GSlider)GetChildAt(4);
            m_total = (GSlider)GetChildAt(8);
            Init();
        }
        partial void Init();
    }
}