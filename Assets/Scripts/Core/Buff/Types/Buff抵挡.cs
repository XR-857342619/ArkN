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
            foreach (Buff buff in Unit.Buffs)
            {
                if (buff.Unit.Buffs.Any(x => x is Buff可抵挡))
                {
                    if (buff.Duration.value < Duration.value)
                        buff.Finish();
                    else
                        buff.isBlocking = Duration.value - buff.Duration.value;
                }
            }
        }
        public override void Finish()
        {
            foreach (var buff in buffs)
            {
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
