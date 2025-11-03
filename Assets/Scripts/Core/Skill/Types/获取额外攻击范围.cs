using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Skills
{
    public class 获取额外攻击范围 : Skill
    {
        public object[] SkillIds;
        public List<Vector2Int> exAttackPoints = new List<Vector2Int>();
        public override void Init()
        {
            base.Init();
            //targetMod = SkillData.Data.GetStr("TargetMod");
            SkillIds = SkillData.Data.GetArray("技能Id列表");
        }

        //public override void Start()
        //{
            //base.Start();
            //Debug.Log("额外攻击范围技能开始");
        //}

        public override void Start()
        {
            //base.Start();
            //Debug.Log("额外攻击范围技能开始");
            if (SkillIds == null || SkillIds.Length == 0) return;
            foreach (object skillId in SkillIds)
            {
                string skillid = Convert.ToString(skillId);
                if (skillid == "") continue;
                Skill skill = Unit.Skills.Find(s => s.SkillData.Id == skillid);
                if (skill == null) continue;
                skill.EXAttackPoints.Clear();
                skill.EXAttackPoints.AddRange(GetExAttackPoints());
                skill.UpdateAttackPoints();
            }
        }
        protected override void OnOpenEnd()
        {
            base.OnOpenEnd();
            if (SkillIds == null || SkillIds.Length == 0) return;
            foreach (object skillId in SkillIds)
            {
                string skillid = Convert.ToString(skillId);
                if (skillid == "") continue;
                Skill skill = Unit.Skills.Find(s => s.SkillData.Id == skillid);
                if (skill == null) continue;
                skill.EXAttackPoints.RemoveAll(p => exAttackPoints.Contains(p));
                skill.UpdateAttackPoints();
            }
        exAttackPoints.Clear();
        }
        public List<Vector2Int> GetExAttackPoints()
        {
            List<Vector2Int> points = new List<Vector2Int>();
            FindTarget();
            //Debug.Log("额外攻击范围技能获取额外攻击点:" + string.Join(",", Targets.Select(t => t.GridPos)));
            foreach (Unit target in Targets)
            {
                //Debug.Log(target.GridPos);
                points.Add(target.GridPos);
            }
            exAttackPoints.AddRange(points);
            return points;
        }
    }
}
