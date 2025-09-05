using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Modifys
{
    public class 穿甲 : Modify, IDamageModify
    {
        public float Chance,Value,Rate;
        public override void Init()
        {
            base.Init();
            Chance = ModifyData.Data.GetFloat("Chance");
            Value = ModifyData.Data.GetFloat("Value");
            Rate = ModifyData.Data.GetFloat("Rate");
        }

        public void Modify(DamageInfo damageInfo)
        {
            if (Chance >= 1 || Battle.Random.NextDouble() < Chance)
            {
                damageInfo.DefIgnore += Value;
                damageInfo.DefIgnoreRate += Rate;
            }
        }
    }
}
