using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FairyGUI;
using UnityEngine;

namespace MainUI
{
    partial class UI_Team : IGameUIView
    {
        GameData gameData => GameData.Instance;
        protected UI_TeamUnit[] teamUnits = new UI_TeamUnit[13];
        protected GButton[] teamBtns = new GButton[4];
        public int TeamIndex;

        //public string backPage;
        //public string BattleLevel;

        TaskCompletionSource<int> tcs;
        partial void Init()
        {
            for (int i = 0; i < teamUnits.Length; i++)
            {
                int k = i;
                teamUnits[i] = GetChild("u" + i) as UI_TeamUnit;
                teamUnits[i].onClick.Add(() =>
                {
                    var selectTeam = UIManager.Instance.ChangeView<UI_TeamSelect>(UI_TeamSelect.URL);
                    var team = gameData.Teams[TeamIndex];
                    //var team = gameData?.Teams[TeamIndex] is null ? new Team() : gameData.Teams[TeamIndex];
                    selectTeam.ChangeUnit(team, teamUnits[k].Card);
                });
            }

            for (int i = 0; i < teamBtns.Length; i++)
            {
                teamBtns[i] = GetChild("team" + i).asButton;
                int k = i;
                teamBtns[i].onClick.Add(() =>
                {
                    TeamIndex = k;
                    Flush();
                });
            }
            m_back.onClick.Add(() =>
            {
                tcs?.TrySetResult(-1);
                //UIManager.Instance.ChangeView<GComponent>(backPage);
            });
            m_quickTeam.onClick.Add(() =>
            {
                var selectTeam = UIManager.Instance.ChangeView<UI_TeamSelect>(UI_TeamSelect.URL);
                var team = gameData?.Teams[TeamIndex] is null ? new Team() : gameData.Teams[TeamIndex];
                selectTeam.QuickSelect(team);
            });
            m_right.onClick.Add(() =>
            {
                TeamIndex = (TeamIndex + 1) % gameData.Teams.Length;
                Flush();
            });
            m_left.onClick.Add(() =>
            {
                TeamIndex = (TeamIndex - 1);
                if (TeamIndex<0) TeamIndex+= gameData.Teams.Length;
                Flush();
            });
            m_delete.onClick.Add(() =>
            {
                gameData.Teams[TeamIndex].Cards.Clear();
                gameData.Teams[TeamIndex].UnitSkill.Clear();
                Flush();
            });
            m_support.onClick.Add(() =>
            {
                TipManager.Instance.ShowTip("不支持友招");
            });
            m_battle.onClick.Add(() => tcs.TrySetResult(TeamIndex));
        }

        public void IfGoBattle(bool bo)
        {
            if (bo)
            {
                m_goBattle.selectedIndex = 0;
            }
            else
            {
                m_goBattle.selectedIndex = 1;
            }
        }

        public void Enter()
        {
            Flush();
        }

        public void Flush()
        {
            var cards = gameData?.Teams[TeamIndex]?.Cards ?? new List<Card>();
            for (int i = 0; i < teamBtns.Length; i++)
            {
                teamBtns[i].selected = i != TeamIndex;
            }
            for (int i = 0; i < teamUnits.Length; i++)
            {
                
                if (i < cards.Count)
                {
                    teamUnits[i].SetCard(cards[i], gameData.Teams[TeamIndex].UnitSkill[i]);
                }
                else
                {
                    teamUnits[i].SetCard(null, 0);
                }
            }
        }

        public async Task<int> ChooseTeam()
        {
            tcs = new TaskCompletionSource<int>();
            var result= await tcs.Task;
            SaveHelper.SaveData();
            return result;
            //return TeamIndex;
        }
    }
}
