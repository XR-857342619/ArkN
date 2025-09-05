/** This is an automatically generated class by FairyGUI. Please do not modify it. **/

using FairyGUI;
using FairyGUI.Utils;

namespace BattleUI
{
    public partial class UI_GiveUP : GComponent
    {
        public GTextField m_title;
        public const string URL = "ui://vp312gabgbwm4x";

        public static UI_GiveUP CreateInstance()
        {
            return (UI_GiveUP)UIPackage.CreateObject("BattleUI", "GiveUP");
        }

        public override void ConstructFromXML(XML xml)
        {
            base.ConstructFromXML(xml);

            m_title = (GTextField)GetChildAt(1);
            Init();
        }
        partial void Init();
    }
}