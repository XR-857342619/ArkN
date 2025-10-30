using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Units;
using UnityEngine;
using static EnemyInfoExcelTool;

namespace Skills
{
    public class ew3类部署干员 : 部署干员
    {
        //public 干员 skilloprator;
        //public Vector2Int pos;
        //public new DirectionEnum direction = DirectionEnum.Right;
        //public Vector3 pos = new Vector3(float.MaxValue, 0, float.MaxValue);
        public Vector3 pos;
        public float r;
        public override void Init()
        {
            base.Init();
            r = SkillData.Data.GetFloat("半径", 0);
        }
        public override void Start()
        {
            //base.Start();
            //FindTarget();
            //Debug.Log(Targets?.First()?.Position);
            pos = GetPos();
            if (pos == new Vector3(float.MaxValue, 0, float.MaxValue)) return;

            List<Unit> battleOp = Battle.AllUnits.FindAll(x => x.UnitData.Id == unitId);
            List<Vector2Int> tilesPos = new List<Vector2Int>();
            
            if (r > 0) tilesPos = GetTilesFromCirle(new Vector2Int((int)pos.x, (int)pos.z), r);
            if (SkillData.AttackPoints.Length > 0) tilesPos.AddRange(GetTilesFromAttackPoints(new Vector2Int((int)pos.x, (int)pos.z)));

            List<Tile> tiles = GetTile(pos, tilesPos, Database.Instance.Get<UnitData>(unitId), count);

            for (int i = 0; i < tiles.Count; i++)
            {
                Unit nowOp = null;
                if (battleOp.Count > 0 && battleOp.Count < i)
                    nowOp = battleOp[i];
                GetToken(nowOp);
                SetToken(tiles[i], nowOp);
            }

        }

        public void SetToken(Tile tile, Unit battleOp = null)
        {
            Debug.Log("获取到部署位置:" + tile.Pos + " 方向:" + direction);
            //Tile tile = Battle.Map.Tiles[(int)pos.x, (int)pos.z];
            Unit toRemove = null;
            toRemove = tile.Units.Where(x => !x.UnitData.NotUseTile).FirstOrDefault();
            if (toRemove is not null && toRemove is Units.干员 toRemoveOprator)
                //if (toRemove is not null)
                toRemoveOprator.LeaveMap();
            if (tile.CanSet(Operator.UnitData))
            {
                Log.Debug("部署干员:" + Operator.UnitData.Name + "于" + tile.Pos);
                //Log.Debug(Operator.Skills.Count());
                //GameObject go = Operator.UnitModel.gameObject;
                //go.transform.position = new Vector3(pos.x, 0.5f, pos.z);
                if (Operator.UnitData.MainSkill is not null && Operator.UnitData.MainSkill.Count() >= 0 && Operator.MainSkill is null)
                    Operator.MainSkill = Operator.LearnSkill(Operator.UnitData.MainSkill[mainSkillId], null);
                Operator.ChangePos((int)tile.Pos.x, (int)tile.Pos.z, direction);
                Operator.JoinMap();
                //tile.Units.Add(Operator);
            }
            else
            {
                if (toRemove is not null && toRemove is Units.干员 RemovedOperator)
                    tile.Units.Add(RemovedOperator);
                if (battleOp is not null)
                    Operator.NowGrid.Units.Add(Operator);
                Log.Debug("无法部署干员:" + Operator.UnitData.Name + "于" + tile.Pos);
                return;
            }
        }

        public List<Tile> GetTile(Vector3 targetPos, List<Vector2Int> tilesPos, UnitData opData, int count)
        {
            List<Tile> result = tilesPos.Select(p => Battle.Map.Tiles[p.x, p.y]).ToList();
            result.RemoveAll(p => !p.CanSet(opData));

            result.Sort((a, b) => Vector3.Distance(a.Pos, targetPos).CompareTo(Vector3.Distance(b.Pos, targetPos)));
            result = result.Take(count).ToList();
            return result;
        }
        public List<Vector2Int> GetTilesFromCirle(Vector2Int center, float radius)
        {
            List<Vector2Int> result = new List<Vector2Int>();
            if (radius <= 0.5f) return new List<Vector2Int>() { center };
            int minX = (int)Math.Floor(center.x - radius);
            int maxX = (int)Math.Ceiling(center.x + radius);
            int minY = (int)Math.Floor(center.y - radius);
            int maxY = (int)Math.Ceiling(center.y + radius);

            float radiusSquared = radius * radius;

            for (int x = minX; x <= maxX; x++)
            {
                for (int y = minY; y <= maxY; y++)
                {
                    // 计算当前网格与圆心的平方距离
                    float dx = x - center.x;
                    float dy = y - center.y;
                    float distanceSquared = dx * dx + dy * dy;

                    // 若距离 ≤ 半径，则加入结果
                    if (distanceSquared <= radiusSquared)
                    {
                        result.Add(new Vector2Int(x, y));
                    }
                }
            }
            result.RemoveAll(p => p.x < 0 || p.x >= Battle.Map.Tiles.GetLength(0) || p.y < 0 || p.y >= Battle.Map.Tiles.GetLength(1));
            return result;
        }
        public List<Vector2Int> GetTilesFromAttackPoints(Vector2Int pos)
        {
            List<Vector2Int> result = new List<Vector2Int>();
            if (AttackPoints == null) return result;
            //AttackPoints.Clear();
            foreach (var p in SkillData.AttackPoints)
            {
                Vector2Int point;
                Vector2 direction = Unit.Direction;

                if (direction == Vector2.right)
                    point = pos + p;
                else if (direction == Vector2.left)
                    point = pos - p;
                else if (direction == Vector2.up)
                    point = pos + new Vector2Int(-p.y, p.x);
                else if (direction == Vector2.down)
                    point = pos + new Vector2Int(p.y, -p.x);
                else
                    point = pos + p;

                if (point.x < 0 || point.x >= Battle.Map.Tiles.GetLength(0) || point.y < 0 || point.y >= Battle.Map.Tiles.GetLength(1)) continue;
                result.Add(point);
            }
            return result;
        }
        public Vector3 GetPos()
        {
            switch (targetPos)
            {
                case "使用自身位置":
                    Debug.Log("useSelfPos:" + Unit.Position);
                    return Unit.Position;
                case "使用附加技能索敌位置":
                    if (SkillData.Skills.Count() > 0)
                    {
                        var skill = Unit.LearnSkill(SkillData.Skills[0]);
                        skill.Init();
                        List<Unit> targets = skill.GetAttackTarget();
                        if (targets.Count > 0)
                        {
                            return targets.FirstOrDefault()?.Position ?? new Vector3(float.MaxValue, 0, float.MaxValue);
                        }
                    }
                    else
                    {
                        return Unit.Position;
                    }
                    Debug.Log("useTargetPos:");
                    break;
                //case "使用干员攻击范围位置":
                //    foreach (var point in AttackPoints)
                //    {
                //        if (point != Unit.Position2)
                //        {
                //            //pos.x = point.x;
                //            //pos.z = point.y;
                //            return new Vector3(point.x, 0, point.y);
                //        }
                //    }
                //    Debug.Log("useAttackPoint:");
                //    break;
                case "使用本技能索敌位置":
                    //Debug.Log(Targets.FirstOrDefault()?.Position ?? new Vector3(float.MaxValue, 0, float.MaxValue));
                    return Targets.FirstOrDefault()?.Position ?? new Vector3(float.MaxValue, 0, float.MaxValue);
            }
            return new Vector3(float.MaxValue, 0, float.MaxValue);
        }
    }
}
