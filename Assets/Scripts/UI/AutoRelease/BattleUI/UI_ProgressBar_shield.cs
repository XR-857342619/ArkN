/** This is an automatically generated class by FairyGUI. Please do not modify it. **/

using FairyGUI;
using FairyGUI.Utils;

namespace BattleUI
{
    public partial class UI_ProgressBar_shield : GProgressBar
    {
        public GImage m_back;
        public const string URL = "ui://vp312gabhwogsk";

        public static UI_ProgressBar_shield CreateInstance()
        {
            return (UI_ProgressBar_shield)UIPackage.CreateObject("BattleUI", "ProgressBar_shield");
        }

        public override void ConstructFromXML(XML xml)
        {
            base.ConstructFromXML(xml);

            m_back = (GImage)GetChildAt(0);
            Init();
        }
        partial void Init();
    }
}