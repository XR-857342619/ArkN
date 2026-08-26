using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Buffs
{
    public class 叠层转化 : MultiLevelBuff
    {
        //public int level;
        //public int maxLevel;
        public string BuffID;
        public float Lasting;

        public override void Init()
        {
            //IsMultiLevel = true;
            base.Init();
            level = 1;
            maxLevel = BuffData.Data.GetInt("maxLevel", 1);
            BuffID = BuffData.Data.GetStr("BuffId");
            Lasting = BuffData.Data.GetFloat("LastTime", 0);
        }

        public override void Reset()
        {
            base.Reset();
            level++;

            //Log.Debug("叠层转化buff升级到" + level + "最大层数为" + maxLevel);

            if (level > maxLevel && maxLevel >= 1)
            {
                Unit.AddBuff(Database.Instance.GetIndex<BuffData>(BuffID), Skill, 0, Lasting);
                Finish();
            }
        }

        public override void Finish()
        {
            base.Finish();
            level = 1;
        }
    }
}
