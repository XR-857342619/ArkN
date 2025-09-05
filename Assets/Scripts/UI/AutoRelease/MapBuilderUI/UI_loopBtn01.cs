/** This is an automatically generated class by FairyGUI. Please do not modify it. **/

using FairyGUI;
using FairyGUI.Utils;

namespace MapBuilderUI
{
    public partial class UI_loopBtn01 : GButton
    {
        public Controller m_mode;
        public const string URL = "ui://wof4wytzfji9r6x";

        public static UI_loopBtn01 CreateInstance()
        {
            return (UI_loopBtn01)UIPackage.CreateObject("MapBuilderUI", "loopBtn01");
        }

        public override void ConstructFromXML(XML xml)
        {
            base.ConstructFromXML(xml);

            m_mode = GetControllerAt(1);
            Init();
        }
        partial void Init();
    }
}