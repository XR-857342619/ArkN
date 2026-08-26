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
        public Vector2Int pos;
        public float r;

        public override void Init()
        {
            base.Init();
            r = SkillData.Data.GetFloat("半径", 0);
        }

        public override void SpSkillEffect()
        {
            pos = GetPos();
            if (pos == new Vector2Int(int.MaxValue, int.MaxValue)) return;

            List<Unit> battleOp = Battle.AllUnits.FindAll(x => x.UnitData.Id == unitId && x.InputTime >= 0);
            List<Vector2Int> tilesPos = new List<Vector2Int>();

            if (r > 0) tilesPos = GetTilesFromCirle(new Vector2Int(pos.x, pos.y), r);
            tilesPos.AddRange(GetTilesFromAttackPoints(new Vector2Int(pos.x, pos.y)));

            List<Tile> tiles = GetTile(pos, tilesPos, Database.Instance.Get<UnitData>(unitId), count);
            Debug.Log($"tiles.Count: {tiles.Count}");

            for (int i = 0; i < tiles.Count; i++)
            {
                Unit nowOp = null;
                if (battleOp.Count > i)
                    nowOp = battleOp[i];

                GetToken(nowOp);
                if (Operator is not null)
                    SetToken(tiles[i], nowOp);
            }
        }

        public void SetToken(Tile tile, Unit battleOp = null)
        {
            if (Operator == null)
            {
                Log.Debug("部署干员失败: Operator 为空");
                return;
            }

            Debug.Log("获取到部署位置:" + tile.Pos + " 方向:" + direction);

            Unit toRemove = null;
            干员 toRemoveOp = null;
            toRemove = tile.Units.Where(x => !x.UnitData.NotUseTile).FirstOrDefault();
            if (toRemove is Units.干员 toRemoveOprator && setMod == "替换")
            {
                tile.Units.Remove(toRemove);
                toRemoveOp = toRemoveOprator;
            }

            if (tile.CanSet(Operator.UnitData))
            {
                Log.Debug("部署干员:" + Operator.UnitData.Name + "于" + tile.Pos);

                if (setMod == "替换" && toRemoveOp is not null)
                    toRemoveOp.LeaveMap(noEvent: true);

                if (Operator.UnitData.MainSkill is not null &&
                    Operator.UnitData.MainSkill.Length > 0 &&
                    Operator.MainSkill is null)
                {
                    int skillIndex = Mathf.Clamp(mainSkillId, 0, Operator.UnitData.MainSkill.Length - 1);
                    Operator.MainSkill = Operator.LearnSkill(Operator.UnitData.MainSkill[skillIndex], null);
                }

                Operator.ChangePos((int)tile.Pos.x, (int)tile.Pos.z, direction);
                Operator.JoinMap(true);
            }
            else
            {
                if (setMod == "替换" && toRemoveOp is not null && toRemove != null)
                    tile.Units.Add(toRemove);

                if (setMod == "位移" && Operator.NowGrid != null)
                    Operator.NowGrid.Units.Add(Operator);

                Log.Debug("无法部署干员:" + Operator.UnitData.Name + "于" + tile.Pos);
            }
        }

        public List<Tile> GetTile(Vector2Int targetPos, List<Vector2Int> tilesPos, UnitData opData, int count)
        {
            List<Tile> result = tilesPos.Select(p => Battle.Map.Tiles[p.x, p.y]).ToList();
            result.RemoveAll(p => !p.CanSet(opData));

            result.Sort((a, b) => Vector2.Distance(a.Pos.ToV2(), targetPos).CompareTo(Vector2.Distance(b.Pos.ToV2(), targetPos)));
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
                    float dx = x - center.x;
                    float dy = y - center.y;
                    float distanceSquared = dx * dx + dy * dy;

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
            if (SkillData.AttackPoints == null) return result;

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

        public Vector2Int GetPos()
        {
            FindTarget();

            switch (targetPos)
            {
                case "使用自身位置":
                    return Unit.GridPos;

                case "使用附加技能索敌位置":
                    if (SkillData.Skills is not null && SkillData.Skills.Length > 0)
                    {
                        var skill = Unit.LearnSkill(SkillData.Skills[0]);
                        skill.Init();
                        List<Unit> targets = skill.GetAttackTarget();
                        if (targets.Count > 0)
                        {
                            return targets.FirstOrDefault()?.GridPos ?? new Vector2Int(int.MaxValue, int.MaxValue);
                        }
                    }
                    else
                    {
                        return Unit.GridPos;
                    }
                    break;

                case "使用本技能索敌位置":
                    return Targets.FirstOrDefault()?.GridPos ?? new Vector2Int(int.MaxValue, int.MaxValue);
            }

            return new Vector2Int(int.MaxValue, int.MaxValue);
        }
    }
}