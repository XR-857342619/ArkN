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
    public class ew3类部署装置 : ew3类部署干员
    {
        public Vector2 directionV2;
        public float lifeTime;
        public new Units.中立单位 Operator;

        public override void Init()
        {
            base.Init();
            lifeTime = SkillData.Data.GetFloat("持续", 0);
        }

        public override void SpSkillEffect()
        {
            pos = GetPos();
            if (pos == new Vector2Int(int.MaxValue, int.MaxValue)) return;

            if (targetDirection == "使用指定单位方向")
            {
                if (Battle.AllUnits.Find(x => x.UnitData.Name == name) is Units.干员 skilloprator)
                    direction = skilloprator.Direction_E;
            }

            directionV2 = DirectionHelper.DirectionToInt(direction);

            List<Vector2Int> tilesPos = new List<Vector2Int>();

            if (r > 0) tilesPos = GetTilesFromCirle(new Vector2Int(pos.x, pos.y), r);
            tilesPos.AddRange(GetTilesFromAttackPoints(new Vector2Int(pos.x, pos.y)));

            List<Tile> tiles = GetTile(pos, tilesPos, count);

            for (int i = 0; i < tiles.Count; i++)
            {
                SetToken(tiles[i].Pos, directionV2, lifeTime);
            }
        }

        public List<Tile> GetTile(Vector2Int targetPos, List<Vector2Int> tilesPos, int count)
        {
            List<Tile> result = tilesPos.Select(p => Battle.Map.Tiles[p.x, p.y]).ToList();
            result.Sort((a, b) => Vector2.Distance(a.Pos.ToV2(), targetPos).CompareTo(Vector2.Distance(b.Pos.ToV2(), targetPos)));
            result = result.Take(count).ToList();
            return result;
        }

        public void SetToken(Vector3 pos, Vector2 direction, float lifeTime = 0)
        {
            int x = Mathf.RoundToInt(pos.x);
            int z = Mathf.RoundToInt(pos.z);

            if (x < 0 || z < 0 ||
                x >= Battle.Map.Tiles.GetLength(0) ||
                z >= Battle.Map.Tiles.GetLength(1))
                return;

            Tile tile = Battle.Map.Tiles[x, z];

            Unit existing = tile.Units.FirstOrDefault(u => u.UnitData.Id == unitId);
            if (existing == null)
                existing = tile.MidUnits.FirstOrDefault(u => u.UnitData.Id == unitId);

            if (existing != null)
            {
                if (setMod != "替换")
                    return;

                existing.Finish(true);
            }

            Operator = Battle.CreateSceneUnit(unitId, pos, direction, lifeTime) as 中立单位;
            if (Operator == null)
            {
                Log.Debug("创建装置失败:" + unitId);
                return;
            }

            Operator.Parent = Unit;
            if (!Unit.Children.Contains(Operator))
                Unit.Children.Add(Operator);
        }
    }
}