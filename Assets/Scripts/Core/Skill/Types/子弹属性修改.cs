using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Skills
{
    public class 子弹属性修改:Skill
    {
        public HashSet<Bullet> bullets = new HashSet<Bullet>();
        public override void Cast()
        {
            base.Cast();
            if (AttackPoints.Count > 0)
            {
                foreach (var pos in AttackPoints)
                {
                    bullets.UnionWith(Battle.FindAllBullets(pos));
                }
            }
            if (SkillData.AttackRange > 0)
            {
                bullets.UnionWith(Battle.FindAllBullets(Unit.Position, SkillData.AttackRange));
            }
            //bullets = bullets.Distinct().ToList();
            if (bullets.Count == 0) return;


        }
        protected virtual void addBuff(Bullet target)
        {
            if (SkillData.Buffs is null) return;
            for (int i = 0; i < SkillData.Buffs.Length; i++)
            {
                var buffChance = 0f;
                if (SkillData.BuffChance != null && SkillData.BuffChance.Length > i) buffChance = SkillData.BuffChance[i];
                if (buffChance == 0 || Battle.Random.NextDouble() < buffChance)
                {
                    int buffId = SkillData.Buffs[i];
                    target.AddBuff(buffId, this, i);
                }
            }
        }
    }
}
