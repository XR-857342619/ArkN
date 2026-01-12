using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Buffs
{
    public class 叠层转化 : Buff
    {
        public int Level;
        public int MaxLevel;
        public string BuffID;
        public float Lasting;

        public override void Init()
        {
            base.Init();
            Level = 1;
            MaxLevel = BuffData.Data.GetInt("MaxLevel", 1);
            BuffID = BuffData.Data.GetStr("BuffId");
            Lasting = BuffData.Data.GetFloat("LastTime", 0);
        }

        public override void Reset()
        {
            base.Reset();
            Level++;

            Log.Debug("叠层转化buff升级到" + Level + "最大层数为" + MaxLevel);

            if (Level > MaxLevel && MaxLevel != 0)
            {
                Unit.AddBuff(Database.Instance.GetIndex<BuffData>(BuffID), Skill, 0, Lasting);
                Finish();
            }
        }
    }
}
