/** This is an automatically generated class by FairyGUI. Please do not modify it. **/

using FairyGUI;
using FairyGUI.Utils;

namespace BattleUI
{
    public partial class UI_Hp2 : GProgressBar
    {
        public Controller m_ShowDetail;
        public const string URL = "ui://vp312gabh4sa45";

        public static UI_Hp2 CreateInstance()
        {
            return (UI_Hp2)UIPackage.CreateObject("BattleUI", "Hp2");
        }

        public override void ConstructFromXML(XML xml)
        {
            base.ConstructFromXML(xml);

            m_ShowDetail = GetControllerAt(0);
            Init();
        }
        partial void Init();
    }
}