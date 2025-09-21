using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Skills
{
    public class 获取单位 : Skill
    {
        public int Count;
        public int ChildId;
        public int MaxCount;
        private int NowCount;
        public int MainSkillId;
        public override void Init()
        {
            base.Init();
            Count = SkillData.Data.GetInt("Count");
            ChildId = Database.Instance.GetIndex<UnitData>(SkillData.Data.GetStr("UnitId"));
            MaxCount = SkillData.Data.GetInt("MaxCount");
            MainSkillId = SkillData.Data.GetInt("MainSkillIndex",0);
        }

        public override bool Useable()
        {
            if (MaxCount != 0 && NowCount >= MaxCount) return false;
            return base.Useable();
        }

        public override void Cast()
        {
            NowCount = (Unit as Units.干员).Children.Where(x => x.InputTime < 0 && x.UnitData.Id == SkillData.Data.GetStr("UnitId")).Count();
            if (MaxCount != 0 && NowCount >= MaxCount) return;
            for (int i = 0; i < Count; i++)
            {
                (Unit as Units.干员).GainChild(ChildId, MainSkillId);
                //Debug.Log(ChildId);
            }
            base.Cast();
        }

        public override void DoOpen()
        {
            NowCount = (Unit as Units.干员).Children.Where(x => x.InputTime < 0 && x.UnitData.Id == SkillData.Data.GetStr("UnitId")).Count();
            if (MaxCount != 0 && NowCount >= MaxCount) return;
            base.DoOpen();
        }
    }
}
