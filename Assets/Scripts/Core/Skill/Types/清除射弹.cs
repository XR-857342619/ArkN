using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Skills
{
    public class 清除射弹 : Skill
    {
        public HashSet<Bullet> bullets = new HashSet<Bullet>();

        public override void Cast()
        {
            base.Cast();

            if (AttackPoints is not null && AttackPoints.Count > 0)
            {
                foreach (var pos in AttackPoints)
                {
                    var foundBullets = Battle.FindAllBullets(pos);
                    foreach (var bullet in foundBullets)
                    {
                        // 只添加敌方子弹
                        if (IsEnemyBullet(bullet))
                        {
                            bullets.Add(bullet);
                        }
                    }
                }
            }

            // 收集所有符合条件的子弹

            if (SkillData.AttackRange > 0)
            {
                var foundBullets = Battle.FindAllBullets(Unit.Position, SkillData.AttackRange);
                foreach (var bullet in foundBullets)
                {
                    // 只添加敌方子弹
                    if (IsEnemyBullet(bullet))
                    {
                        bullets.Add(bullet);
                    }
                }
            }

            // 处理所有收集到的敌方子弹
            foreach (var bullet in bullets)
            {
                // 从战场中直接移除子弹
                Battle.Bullets.Remove(bullet);

                // 回收子弹模型
                if (bullet.BulletModel != null)
                {
                    BulletManager.Instance.Return(bullet.BulletModel);
                    bullet.BulletModel = null;
                }
            }

            // 清空集合
            bullets.Clear();
        }

        // 判断子弹是否为敌方子弹的方法
        private bool IsEnemyBullet(Bullet bullet)
        {
            if (bullet.Skill.Unit is Units.敌人)
            {
                return true;
            }
            return false;
        }
    }
}