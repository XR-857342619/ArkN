/** This is an automatically generated class by FairyGUI. Please do not modify it. **/

using FairyGUI;
using FairyGUI.Utils;

namespace MapBuilderUI
{
    public partial class UI_WavePage : GComponent
    {
        public Controller m_selectEnemy;
        public Controller m_Hide;
        public Controller m_Preview;
        public Controller m_PreviewBtn;
        public GButton m_AddWave;
        public GList m_wavwList;
        public GButton m_DeleteWave;
        public GButton m_CopyWave;
        public GGraph m_selectBack;
        public GList m_filterList;
        public GTextInput m_filterName;
        public GButton m_ExistOnly;
        public GButton m_MidOnly;
        public GButton m_Hide_2;
        public GSlider m_progressBar;
        public GButton m_playBtn;
        public GSlider m_playSpeed;
        public GButton m_perview;
        public UI_loopBtn01 m_loopBtn01;
        public GGroup m_play;
        public const string URL = "ui://wof4wytzq2unc";

        public static UI_WavePage CreateInstance()
        {
            return (UI_WavePage)UIPackage.CreateObject("MapBuilderUI", "WavePage");
        }

        public override void ConstructFromXML(XML xml)
        {
            base.ConstructFromXML(xml);

            m_selectEnemy = GetControllerAt(0);
            m_Hide = GetControllerAt(1);
            m_Preview = GetControllerAt(2);
            m_PreviewBtn = GetControllerAt(3);
            m_AddWave = (GButton)GetChildAt(1);
            m_wavwList = (GList)GetChildAt(2);
            m_DeleteWave = (GButton)GetChildAt(3);
            m_CopyWave = (GButton)GetChildAt(4);
            m_selectBack = (GGraph)GetChildAt(5);
            m_filterList = (GList)GetChildAt(7);
            m_filterName = (GTextInput)GetChildAt(9);
            m_ExistOnly = (GButton)GetChildAt(13);
            m_MidOnly = (GButton)GetChildAt(15);
            m_Hide_2 = (GButton)GetChildAt(17);
            m_progressBar = (GSlider)GetChildAt(18);
            m_playBtn = (GButton)GetChildAt(19);
            m_playSpeed = (GSlider)GetChildAt(20);
            m_perview = (GButton)GetChildAt(21);
            m_loopBtn01 = (UI_loopBtn01)GetChildAt(22);
            m_play = (GGroup)GetChildAt(23);
            Init();
        }
        partial void Init();
    }
}