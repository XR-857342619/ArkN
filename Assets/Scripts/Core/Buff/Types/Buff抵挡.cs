using UnityEngine;
using System.Linq;
using System.Collections.Generic;

namespace Buffs
{
    public class Buff抵挡 : Buff
    {
        public List<object[]> buffs = new List<object[]>();
        
        public override void Init()
        {
            base.Init();
            //foreach (Buff buff in Unit.Buffs)
            for (int i = Unit.Buffs.Count; i > 0; i--)
            {
                Buff buff = Unit.Buffs[i - 1];
                if (buff.Unit == null) continue;
                if (buff.Unit.Buffs.Any(x => x is Buff可抵挡))
                {
                    if (buff.Duration.value < Duration.value) buff.Finish();
                    else buff.isBlocking = Duration.value - buff.Duration.value;
                }
            }
        }
        public override void Finish()
        {
            foreach (var buff in buffs)
            {
                //Log.Debug(Unit.UnitData.Name + "重新获得了" + buff[1] + "持续" + buff[3] + "秒");
                Unit.AddBuff((int)buff[0], (Skill)buff[1], (int)buff[2], (float)buff[3]);
            }
            base.Finish();
        }

        public void AddBuff(object[] buffInfo)
        {
            buffs.Add(buffInfo);
        }
    }
}
