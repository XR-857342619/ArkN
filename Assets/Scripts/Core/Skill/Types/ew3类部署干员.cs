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
    public class ew3类部署干员 : Skill
    {
        public 干员 Operator;
        //public 干员 skilloprator;
        //public Vector2Int pos;
        public DirectionEnum direction = DirectionEnum.Right;
        //public Vector3 pos = new Vector3(float.MaxValue, 0, float.MaxValue);
        public Vector3 pos;
        public string targetDirection;
        public string setMod;
        public string targetPos;
        public string name;
        public int mainSkillId;
        public override void Init()
        {
            base.Init();
            mainSkillId = SkillData.Data.GetInt("召唤物主技能索引", 0);
            //targetPos = SkillData.Data.GetStr("召唤位置", "");
            //targetDirection = SkillData.Data.GetStr("召唤物方向", "固定方向");
            //setMod = SkillData.Data.GetStr("部署模式", "追加");
            //if (targetDirection == "固定方向")
                //Enum.TryParse(SkillData.Data.GetStr("方向"), out direction);
        }
        public override void Start()
        {
            //base.Start();
            
            string unitId = SkillData.Data.GetStr("召唤物ID");
            Operator = Battle.CreatePlayerUnit(Database.Instance.GetIndex<UnitData>(unitId)) as 干员;
            Operator.Parent = Unit;
            Unit.Children.Add(Operator);

            var tilesPos = GetTilesFromCirle(Unit.GridPos, SkillData.AttackRange).Union(SkillData.AttackPoints);
            Tile tile = GetTile(tilesPos.ToList(), Operator);

            if (tile == null) return;

            Debug.Log("获取到部署位置:" + tile.X + "," + tile.Y);

            Log.Debug("部署干员:" + Operator.UnitData.Name + "于" + pos);
            //Log.Debug(Operator.Skills.Count());

            if (Operator.UnitData.MainSkill is not null && Operator.UnitData.MainSkill.Count() >= 0)
                Operator.MainSkill = Operator.LearnSkill(Operator.UnitData.MainSkill[mainSkillId], null);
            Operator.ChangePos((int)pos.x, (int)pos.z, direction);
            Operator.JoinMap();
            Operator.Parent = Battle.AllUnits.Find(x => x.UnitData.Name == name) as Units.干员 ?? null;
            //tile.Units.Add(Operator);
        }
        public Tile GetTile(List<Vector2Int> tilesPos, Unit op)
        {
            Tile result = Battle.Map.Tiles[tilesPos[0].x, tilesPos[0].y];
            foreach (Vector2Int pos in tilesPos)
            {
                Tile tile = Battle.Map.Tiles[pos.x, pos.y];
                if (tile.CanSet(op, op.UnitData.NotUseTile) && (pos - Unit.GridPos).sqrMagnitude < (new Vector2Int(result.X, result.Y) - Unit.GridPos).sqrMagnitude)
                    result = tile;
            }
            if (!result.CanSet(op, op.UnitData.NotUseTile)) return null;
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

            return result;
        }
    }
}
