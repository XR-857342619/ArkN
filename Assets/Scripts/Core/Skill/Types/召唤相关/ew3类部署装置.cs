using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Remoting.Lifetime;
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
            lifeTime = SkillData.Data.GetFloat("持续",0);
        }
        public override void Start()
        {
            //base.Start();
            //FindTarget();
            //Debug.Log(Targets?.First()?.Position);
            pos = GetPos();
            if (pos == new Vector3(float.MaxValue, 0, float.MaxValue)) return;

            //List<Unit> battleOp = Battle.AllUnits.FindAll(x => x.UnitData.Id == unitId);

            if (targetDirection == "使用指定单位方向")
            {
                if (Battle.AllUnits.Find(x => x.UnitData.Name == name) is Units.干员 skilloprator)
                    direction = skilloprator.Direction_E;
            }
            directionV2 = DirectionHelper.DirectionToInt(direction);

            List<Vector2Int> tilesPos = new List<Vector2Int>();
            
            if (r > 0) tilesPos = GetTilesFromCirle(new Vector2Int((int)pos.x, (int)pos.z), r);
            if (SkillData.AttackPoints.Length > 0) tilesPos.AddRange(GetTilesFromAttackPoints(new Vector2Int((int)pos.x, (int)pos.z)));

            List<Tile> tiles = GetTile(pos, tilesPos, Database.Instance.Get<UnitData>(unitId), count);

            for (int i = 0; i < tiles.Count; i++)
            {
                SetToken(tiles[i].Pos, directionV2, lifeTime);
            }

        }

        public void SetToken(Vector3 pos, Vector2 direction, float lifeTime = 0)
        {
            Debug.Log("部署装置：" + unitId + " 到：" + pos + " 方向：" + direction + " 持续：" + lifeTime);
            Operator = Battle.CreateSceneUnit(unitId, pos, direction, lifeTime) as 中立单位;
            Operator.Parent = Unit;
            Unit.Children.Add(Operator);
        }
    }
}
