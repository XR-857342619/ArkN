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
        public int MaxLine;
        public float BaseAngle;
        public float FanRange;
        public float GapDistance;
        public float MaxDistance;

        public override void Init()
        {
            base.Init();
            StartPos = SkillData.Data.GetStr("StartPos", defaultValue: "UnitPos");
            Line = SkillData.Data.GetInt("Line", defaultValue: 1);
            MaxLine = SkillData.Data.GetInt("MaxLine", SkillData.Data.GetInt("maxLine", Line));
            BaseAngle = SkillData.Data.GetFloat("BaseAngle",
                SkillData.Data.GetFloat("BaseDirection",
                SkillData.Data.GetFloat("基准方向", 0f)));
            FanRange = SkillData.Data.GetFloat("FanRange",
                SkillData.Data.GetFloat("SectorRange",
                SkillData.Data.GetFloat("扇形范围", 0f)));
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

        /// <summary>
        /// 在按 GapDistance 居中排列的 maxLineCount 条候选直线中，随机选择 fireCount 条。
        /// 仅在 MaxLine != Line 时使用；两者相等时保留原分支逻辑。
        /// </summary>
        private List<float> GetRandomLineOffsets(int fireCount, int maxLineCount, Func<float, bool> isValid = null)
        {
            var result = new List<float>();
            if (fireCount <= 0 || maxLineCount <= 0)
                return result;

            var candidates = new List<float>();
            float center = (maxLineCount - 1) / 2f;
            for (int i = 0; i < maxLineCount; i++)
            {
                float offset = (i - center) * GapDistance;
                if (isValid == null || isValid(offset))
                    candidates.Add(offset);
            }

            for (int i = candidates.Count - 1; i > 0; i--)
            {
                int j = Battle.Random.Next(i + 1);
                float temp = candidates[i];
                candidates[i] = candidates[j];
                candidates[j] = temp;
            }

            int take = Mathf.Min(fireCount, candidates.Count);
            for (int i = 0; i < take; i++)
            {
                result.Add(candidates[i]);
            }
            return result;
        }

        private List<int> GetRandomLineIndexes(int fireCount, int maxLineCount)
        {
            var result = new List<int>();
            if (fireCount <= 0 || maxLineCount <= 0)
                return result;

            var candidates = new List<int>();
            for (int i = 0; i < maxLineCount; i++)
            {
                candidates.Add(i);
            }

            for (int i = candidates.Count - 1; i > 0; i--)
            {
                int j = Battle.Random.Next(i + 1);
                int temp = candidates[i];
                candidates[i] = candidates[j];
                candidates[j] = temp;
            }

            int take = Mathf.Min(fireCount, candidates.Count);
            for (int i = 0; i < take; i++)
            {
                result.Add(candidates[i]);
            }
            return result;
        }

        /// <summary>
        /// 将 xOz 平面方向按顺时针角度旋转。
        /// 角度为正表示以单位面向为基准的顺时针方向。
        /// </summary>
        private Vector3 RotateDirection(Vector2 direction, float angleDegrees)
        {
            Vector2 dir = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
            float rad = angleDegrees * Mathf.Deg2Rad;
            float sin = Mathf.Sin(rad);
            float cos = Mathf.Cos(rad);
            return new Vector3(dir.x * cos - dir.y * sin, 0f, dir.x * sin + dir.y * cos).normalized;
        }

        private void CreateFanBullet(Vector3 startPoint, Unit target, float angleDegrees)
        {
            Vector3 direction = RotateDirection(Unit.Direction, angleDegrees);
            Battle.CreateBullet(SkillData.Bullet.Value, startPoint, startPoint + direction * MaxDistance, target, this);
            Debug.DrawRay(startPoint, direction * MaxDistance, Color.red, 3f);
        }

        private void FireFan(Vector3 startPoint, Unit target)
        {
            if (MaxLine <= 0 || Line <= 0)
                return;

            float fanRange = Mathf.Abs(FanRange);
            float startAngle;
            float angleStep;
            if (MaxLine == 1)
            {
                startAngle = BaseAngle;
                angleStep = 0f;
            }
            else
            {
                startAngle = BaseAngle - fanRange / 2f;
                angleStep = fanRange / (MaxLine - 1);
            }

            if (MaxLine == Line)
            {
                for (int i = 0; i < MaxLine; i++)
                {
                    CreateFanBullet(startPoint, target, startAngle + i * angleStep);
                }
            }
            else
            {
                foreach (int i in GetRandomLineIndexes(Line, MaxLine))
                {
                    CreateFanBullet(startPoint, target, startAngle + i * angleStep);
                }
            }
        }

        public override void Effect(Unit target)
        {
            if (SkillData.Bullet != null)
            {
                if (Mathf.Abs(FanRange) > 0f)
                {
                    Vector3 fanStart = Unit.UnitModel.GetPoint(SkillData.ShootPoint);
                    FireFan(fanStart, target);
                    return;
                }

                //创建一个子弹
                Vector3 startPoint = Vector3.zero;
                // Unit.Direction 为 Vector2 是xOz平面上的方向向量
                switch (StartPos)
                {
                    case "UnitPos":
                        startPoint = Unit.UnitModel.GetPoint(SkillData.ShootPoint);
                        Vector3 perpDir = new Vector3(Unit.Direction.y, 0, Unit.Direction.x);
                        Vector3 direction = new Vector3(Unit.Direction.x, 0, Unit.Direction.y).normalized;

                        if (MaxLine == Line)
                        {
                            float offset = (Line - 1) * GapDistance / 2;
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
                        }
                        else
                        {
                            foreach (var currentOffset in GetRandomLineOffsets(Line, MaxLine))
                            {
                                Vector3 bulletStart = startPoint + perpDir * currentOffset;
                                Battle.CreateBullet(SkillData.Bullet.Value,
                                                  bulletStart,
                                                  bulletStart + direction * MaxDistance,
                                                  target,
                                                  this);
                                // 在调试模式下显示弹道
                                Debug.DrawRay(bulletStart, direction * MaxDistance, Color.red, 3f);
                            }
                        }
                        break;

                    case "MapLeft":
                        startPoint = Unit.NowGrid.Pos;
                        startPoint.x = -2;
                        if (MaxLine == Line)
                        {
                            for (int i = -Line; i <= Line; i++)
                            {
                                var j = startPoint.z + i * GapDistance;
                                if (j >= 0 && j < Battle.Map.Tiles.GetLength(1))
                                    Battle.CreateBullet(SkillData.Bullet.Value, startPoint + i * new Vector3(0, 0, 1), startPoint + new Vector3(1, 0, 0) * MaxDistance + i * new Vector3(0, 0, 1), target, this);
                            }
                        }
                        else
                        {
                            var lineOffsets = GetRandomLineOffsets(Line, MaxLine, candidateOffset =>
                            {
                                var z = startPoint.z + candidateOffset;
                                return z >= 0 && z < Battle.Map.Tiles.GetLength(1);
                            });
                            foreach (var offset in lineOffsets)
                            {
                                var bulletStart = startPoint + new Vector3(0, 0, offset);
                                var bulletEnd = startPoint + new Vector3(1, 0, 0) * MaxDistance + new Vector3(0, 0, offset);
                                Battle.CreateBullet(SkillData.Bullet.Value, bulletStart, bulletEnd, target, this);
                            }
                        }
                        break;
                    case "MapDown":
                        startPoint = Unit.NowGrid.Pos;
                        startPoint.z = -2;
                        if (MaxLine == Line)
                        {
                            for (int i = -Line; i <= Line; i++)
                            {
                                var x = startPoint.x + i * GapDistance;
                                if (x >= 0 && x < Battle.Map.Tiles.GetLength(0))
                                    Battle.CreateBullet(SkillData.Bullet.Value, new Vector3(x, startPoint.y, startPoint.z), new Vector3(x, startPoint.y, startPoint.z + MaxDistance), target, this);
                            }
                        }
                        else
                        {
                            var lineOffsets = GetRandomLineOffsets(Line, MaxLine, candidateOffset =>
                            {
                                var x = startPoint.x + candidateOffset;
                                return x >= 0 && x < Battle.Map.Tiles.GetLength(0);
                            });
                            foreach (var offset in lineOffsets)
                            {
                                var x = startPoint.x + offset;
                                Battle.CreateBullet(SkillData.Bullet.Value, new Vector3(x, startPoint.y, startPoint.z), new Vector3(x, startPoint.y, startPoint.z + MaxDistance), target, this);
                            }
                        }
                        break;
                    case "MapRight":
                        startPoint = Unit.NowGrid.Pos;
                        startPoint.x = Battle.Map.Tiles.GetLength(0) + 2;
                        if (MaxLine == Line)
                        {
                            for (int i = -Line; i <= Line; i++)
                            {
                                var j = startPoint.z + i * GapDistance;
                                if (j >= 0 && j < Battle.Map.Tiles.GetLength(1))
                                    Battle.CreateBullet(SkillData.Bullet.Value, startPoint + i * new Vector3(0, 0, 1), startPoint + new Vector3(-1, 0, 0) * MaxDistance + i * new Vector3(0, 0, 1), target, this);
                            }
                        }
                        else
                        {
                            var lineOffsets = GetRandomLineOffsets(Line, MaxLine, candidateOffset =>
                            {
                                var z = startPoint.z + candidateOffset;
                                return z >= 0 && z < Battle.Map.Tiles.GetLength(1);
                            });
                            foreach (var offset in lineOffsets)
                            {
                                var bulletStart = startPoint + new Vector3(0, 0, offset);
                                var bulletEnd = startPoint + new Vector3(-1, 0, 0) * MaxDistance + new Vector3(0, 0, offset);
                                Battle.CreateBullet(SkillData.Bullet.Value, bulletStart, bulletEnd, target, this);
                            }
                        }
                        break;
                    case "MapUp":
                        startPoint = Unit.NowGrid.Pos;
                        startPoint.z = Battle.Map.Tiles.GetLength(1) + 2;
                        if (MaxLine == Line)
                        {
                            for (int i = -Line; i <= Line; i++)
                            {
                                var x = startPoint.x + i * GapDistance;
                                if (x >= 0 && x < Battle.Map.Tiles.GetLength(0))
                                    Battle.CreateBullet(SkillData.Bullet.Value, new Vector3(x, startPoint.y, startPoint.z), new Vector3(x, startPoint.y, startPoint.z - MaxDistance), target, this);
                            }
                        }
                        else
                        {
                            var lineOffsets = GetRandomLineOffsets(Line, MaxLine, candidateOffset =>
                            {
                                var x = startPoint.x + candidateOffset;
                                return x >= 0 && x < Battle.Map.Tiles.GetLength(0);
                            });
                            foreach (var offset in lineOffsets)
                            {
                                var x = startPoint.x + offset;
                                Battle.CreateBullet(SkillData.Bullet.Value, new Vector3(x, startPoint.y, startPoint.z), new Vector3(x, startPoint.y, startPoint.z - MaxDistance), target, this);
                            }
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
