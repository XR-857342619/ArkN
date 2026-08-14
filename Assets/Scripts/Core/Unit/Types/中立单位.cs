using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static UnityEngine.EventSystems.EventTrigger;

namespace Units
{
    public class 中立单位 : Unit
    {
        public override void UpdateAction()
        {
            //UpdateBuffSuppression();

            if (Alive())
            {
                //HP自动回复
                Hp += HpRecover * SystemConfig.DeltaTime;
                Hp += HpRecoverP * MaxHp * SystemConfig.DeltaTime;
                if (Hp > MaxHp) Hp = MaxHp;
                if (Hp < 0) DoDie(null);
            }
            if (this.State == StateEnum.Die)
            {
                UpdateDie();
            }
            else
            {
                UpdateSkills();
            }
        }

        public override void Init()
        {
            base.Init();
            if (UnitData.MainSkill != null)
                MainSkill = LearnSkill(UnitData.MainSkill[0], null);
            BattleUI.UI_Battle.Instance.CreateUIUnit(this);
            Agi = 100;
            Trigger(TriggerEnum.自己入场);
        }

        public override void Finish(bool leaveEvent = true)
        {
            base.Finish(leaveEvent);
            Battle.AllUnits.Remove(this);
            if (Team == 0) Battle.PlayerUnits2.Remove(this);
            BattleUI.UI_Battle.Instance.ReturnUIUnit(this);
            if (Battle.Map.Tiles[GridPos.x, GridPos.y].Units.Contains(this))
                Battle.Map.Tiles[GridPos.x, GridPos.y].Units.Remove(this);
            if (Battle.Map.Tiles[GridPos.x, GridPos.y].MidUnits.Contains(this))
                Battle.Map.Tiles[GridPos.x, GridPos.y].MidUnits.Remove(this);
            UnityEngine.GameObject.Destroy(UnitModel.gameObject);
            UnitModel = null;
        }

        public void AlignHeight()
        {
            Tile tile = Battle.Map.Tiles[GridPos.x, GridPos.y];
        }
    }
}
