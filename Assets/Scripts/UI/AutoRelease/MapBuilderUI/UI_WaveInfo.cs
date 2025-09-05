/** This is an automatically generated class by FairyGUI. Please do not modify it. **/

using FairyGUI;
using FairyGUI.Utils;

namespace MapBuilderUI
{
    public partial class UI_WaveInfo : GButton
    {
        public GGraph m_headback;
        public GTextField m_name;
        public GTextInput m_delay;
        public GTextInput m_count;
        public GTextInput m_gap;
        public GLoader m_head;
        public GTextInput m_x;
        public GTextInput m_y;
        public GComboBox m_path;
        public GTextInput m_checkPoint;
        public GTextInput m_tag;
        public const string URL = "ui://wof4wytzq2unb";

        public static UI_WaveInfo CreateInstance()
        {
            return (UI_WaveInfo)UIPackage.CreateObject("MapBuilderUI", "WaveInfo");
        }

        public override void ConstructFromXML(XML xml)
        {
            base.ConstructFromXML(xml);

            m_headback = (GGraph)GetChildAt(7);
            m_name = (GTextField)GetChildAt(8);
            m_delay = (GTextInput)GetChildAt(10);
            m_count = (GTextInput)GetChildAt(12);
            m_gap = (GTextInput)GetChildAt(15);
            m_head = (GLoader)GetChildAt(16);
            m_x = (GTextInput)GetChildAt(19);
            m_y = (GTextInput)GetChildAt(20);
            m_path = (GComboBox)GetChildAt(22);
            m_checkPoint = (GTextInput)GetChildAt(24);
            m_tag = (GTextInput)GetChildAt(27);
            Init();
        }
        partial void Init();
    }
}