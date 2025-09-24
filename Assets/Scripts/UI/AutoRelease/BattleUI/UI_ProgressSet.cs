/** This is an automatically generated class by FairyGUI. Please do not modify it. **/

using FairyGUI;
using FairyGUI.Utils;

namespace BattleUI
{
    public partial class UI_ProgressSet : GComponent
    {
        public Controller m_BarType;
        public const string URL = "ui://vp312gabp78xtn";

        public static UI_ProgressSet CreateInstance()
        {
            return (UI_ProgressSet)UIPackage.CreateObject("BattleUI", "ProgressSet");
        }

        public override void ConstructFromXML(XML xml)
        {
            base.ConstructFromXML(xml);

            m_BarType = GetControllerAt(0);
            Init();
        }
        partial void Init();
    }
}