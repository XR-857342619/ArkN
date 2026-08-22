/** This is an automatically generated class by FairyGUI. Please do not modify it. **/

using FairyGUI;
using FairyGUI.Utils;

namespace BattleUI
{
    public partial class UI_CommonBar : GProgressBar
    {
        public Controller m_useControl;
        public Controller m_ShowDetail;
        public const string URL = "ui://vp312gabh4sa43";

        public static UI_CommonBar CreateInstance()
        {
            return (UI_CommonBar)UIPackage.CreateObject("BattleUI", "CommonBar");
        }

        public override void ConstructFromXML(XML xml)
        {
            base.ConstructFromXML(xml);

            m_useControl = GetControllerAt(0);
            m_ShowDetail = GetControllerAt(1);
            Init();
        }
        partial void Init();
    }
}