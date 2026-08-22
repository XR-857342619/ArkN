/** This is an automatically generated class by FairyGUI. Please do not modify it. **/

using FairyGUI;
using FairyGUI.Utils;

namespace BattleUI
{
    public partial class UI_ProgressBar_boss : GProgressBar
    {
        public Controller m_type;
        public const string URL = "ui://vp312gabhwogsq";

        public static UI_ProgressBar_boss CreateInstance()
        {
            return (UI_ProgressBar_boss)UIPackage.CreateObject("BattleUI", "ProgressBar_boss");
        }

        public override void ConstructFromXML(XML xml)
        {
            base.ConstructFromXML(xml);

            m_type = GetControllerAt(0);
            Init();
        }
        partial void Init();
    }
}