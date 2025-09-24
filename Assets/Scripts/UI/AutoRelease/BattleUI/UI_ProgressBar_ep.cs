/** This is an automatically generated class by FairyGUI. Please do not modify it. **/

using FairyGUI;
using FairyGUI.Utils;

namespace BattleUI
{
    public partial class UI_ProgressBar_ep : GProgressBar
    {
        public Controller m_elementType;
        public const string URL = "ui://vp312gabvbbft7";

        public static UI_ProgressBar_ep CreateInstance()
        {
            return (UI_ProgressBar_ep)UIPackage.CreateObject("BattleUI", "ProgressBar_ep");
        }

        public override void ConstructFromXML(XML xml)
        {
            base.ConstructFromXML(xml);

            m_elementType = GetControllerAt(0);
            Init();
        }
        partial void Init();
    }
}