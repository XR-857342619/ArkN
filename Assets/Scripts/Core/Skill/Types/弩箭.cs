using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Skills
{
    public class 弩箭 : 非指向技能
    {
        public string StartPos;
        public int Line;
        public float GapDistance;
        public float MaxDistance;

        public override void Init()
        {
            base.Init();
            StartPos = SkillData.Data.GetStr("StartPos", defaultValue: "UnitPos");
            Line = SkillData.Data.GetInt("Line", defaultValue: 1);
            GapDistance = SkillData.Data.GetFloat("GapDistance", defaultValue: 1f);
            MaxDistance = SkillData.Data.GetFloat("MaxDistance", defaultValue: 20f);
        }

        public override void Start()
        {
            base.Start();
        }

        public override void Cast()
        {
            base.Cast();
        }

        public override void Effect(Unit target)
        {
            if (SkillData.Bullet != null)
            {
                //创建一个子弹
                Vector3 startPoint = Vector3.zero;
                // Unit.Direction 为 Vector2 是xOz平面上的方向向量
                switch (StartPos)
                {
                    case "UnitPos":
                        float offset = Line * GapDistance / 2;
                        startPoint = Unit.UnitModel.GetPoint(SkillData.ShootPoint);
                        Vector3 perpDir = new Vector3(Unit.Direction.y, 0, Unit.Direction.x);
                        Vector3 direction = new Vector3(Unit.Direction.x, 0, Unit.Direction.y).normalized;

                        for (int i = 0; i < Line; i++)
                        {
                            float currentOffset = i * GapDistance - offset;
                            Vector3 bulletStart = startPoint + perpDir * currentOffset;
                            Battle.CreateBullet(SkillData.Bullet.Value,
                                              bulletStart,
                                              bulletStart + direction * MaxDistance,
                                              target,
                                              this);
                            // 在调试模式下显示弹道
                            Debug.DrawRay(bulletStart, direction * MaxDistance, Color.red, 3f);
                        }
                        break;

                    case "MapLeft":
                        startPoint = Unit.NowGrid.Pos;
                        startPoint.x = -2;
                        for (int i = -Line; i <= Line; i++)
                        {
                            var j = startPoint.z + i * GapDistance;
                            if (j >= 0 && j < Battle.Map.Tiles.GetLength(1))
                                Battle.CreateBullet(SkillData.Bullet.Value, startPoint + i * new Vector3(0, 0, 1), startPoint + new Vector3(1, 0, 0) * MaxDistance + i * new Vector3(0, 0, 1), target, this);
                        }
                        break;
                    case "MapDown":
                        startPoint = Unit.NowGrid.Pos;
                        startPoint.z = -2;
                        for (int i = -Line; i <= Line; i++)
                        {
                            var j = startPoint.x + i * GapDistance;
                            if (j >= 0 && j < Battle.Map.Tiles.GetLength(0))
                                Battle.CreateBullet(SkillData.Bullet.Value, startPoint + i * new Vector3(0, 0, 1), startPoint + new Vector3(0, 0, 1) * MaxDistance + i * new Vector3(0, 1, 0), target, this);
                        }
                        break;
                    case "MapRight":
                        startPoint = Unit.NowGrid.Pos;
                        startPoint.x = Battle.Map.Tiles.GetLength(0) + 2;
                        for (int i = -Line; i <= Line; i++)
                        {
                            var j = startPoint.z + i * GapDistance;
                            if (j >= 0 && j < Battle.Map.Tiles.GetLength(1))
                                Battle.CreateBullet(SkillData.Bullet.Value, startPoint + i * new Vector3(0, 0, 1), startPoint + new Vector3(-1, 0, 0) * MaxDistance + i * new Vector3(0, 0, 1), target, this);
                        }
                        break;
                    case "MapUp":
                        startPoint = Unit.NowGrid.Pos;
                        startPoint.x = Battle.Map.Tiles.GetLength(1) + 2;
                        for (int i = -Line; i <= Line; i++)
                        {
                            var j = startPoint.x + i * GapDistance;
                            if (j >= 0 && j < Battle.Map.Tiles.GetLength(0))
                                Battle.CreateBullet(SkillData.Bullet.Value, startPoint + i * new Vector3(1, 0, 0), startPoint + new Vector3(0, 0, -1) * MaxDistance + i * new Vector3(0, 1, 0), target, this);
                        }
                        break;
                    default:
                        break;
                }
                //Debug.Log($"攻击{target.Config.Name}:{target.Hp} 起点：{startPoint}");

            }
        }

        public override void BreakCast()
        {
            base.BreakCast();
        }
    }
}
