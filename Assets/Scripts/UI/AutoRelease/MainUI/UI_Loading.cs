/** This is an automatically generated class by FairyGUI. Please do not modify it. **/

using FairyGUI;
using FairyGUI.Utils;

namespace MainUI
{
    public partial class UI_Loading : GComponent
    {
        public GTextField m_name;
        public GProgressBar m_loadingBar;
        public GTextField m_loadDes;
        public const string URL = "ui://k4mja8t1it6gr51";

        public static UI_Loading CreateInstance()
        {
            return (UI_Loading)UIPackage.CreateObject("MainUI", "Loading");
        }

        public override void ConstructFromXML(XML xml)
        {
            base.ConstructFromXML(xml);

            m_name = (GTextField)GetChildAt(1);
            m_loadingBar = (GProgressBar)GetChildAt(3);
            m_loadDes = (GTextField)GetChildAt(4);
            Init();
        }
        partial void Init();
    }
}