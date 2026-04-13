using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
//using UnityEngine;

namespace Skills
{
    public class 地块属性更改 : Skill
    {
        //CountDown c = new CountDown();
        public float Passable = 0;
        public float PassCostAdd = 0;
        public override void Init()
        {
            base.Init();
            Passable = SkillData.Data.GetFloat("Passable");
            PassCostAdd = SkillData.Data.GetFloat("PassCostAdd");
        }

        public override void Update()
        {
            base.Update();
            /*延迟几帧进行重寻路防止出bug*/
            //if (c.Update(SystemConfig.DeltaTime))
            //{
                
            //}
        }

        public override void Cast()
        {
            //base.Cast();
            //List<Tile> t = null;
            Log.Debug("地块:"+Targets.Count);
            if (Targets.Count <= 0) return;

            foreach (var target in Targets)
            {
                Tile tile = Unit.Battle.Map.Tiles[target.GridPos.x, target.GridPos.y];
                if (Passable != 0)
                {
                    tile.Passable = Passable == 1 ? true : false;
                }
                tile.PassCost += PassCostAdd;
                Log.Debug(target.GridPos + "地块属性修改成功！" + "Passable:" + tile.Passable + " PassCost:" + tile.PassCost);
            }
            //由于地块通行性发生变化，通知所有敌人
            foreach (var unit in Battle.Enemys)
            {
                Log.Debug("地块属性变更");
                (unit as Units.敌人).NeedResetPath = true;
                (unit as Units.敌人).OnlyCheckPoint = true;
            }
        }
    }
}
