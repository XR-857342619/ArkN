using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Units;
using UnityEngine;
using static EnemyInfoExcelTool;
using static UnityEngine.UI.CanvasScaler;

namespace Skills
{
    public class 部署装置 : 部署干员
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
            posList = GetPosList();
            if (posList.Count == 0)
            {
                return;
            }

            if (targetDirection == "使用指定单位方向")
            {
                if (Battle.AllUnits.Find(x => x.UnitData.Name == name) is Units.干员 skilloprator)
                    direction = skilloprator.Direction_E;
            }

            directionV2 = DirectionHelper.DirectionToInt(direction);

            for (int i = 0; i < posList.Count; i++)
            {
                if (i >= count) break;
                SetToken(posList[i], directionV2, lifeTime);
            }
        }

        public virtual void SetToken(Vector2Int pos, Vector2 direction, float lifeTime = 0)
        {
            if (pos.x < 0 || pos.y < 0 ||
                pos.x >= Battle.Map.Tiles.GetLength(0) ||
                pos.y >= Battle.Map.Tiles.GetLength(1))
                return;

            Tile tile = Battle.Map.Tiles[pos.x, pos.y];

            // 已有同 ID 装置：非替换模式直接跳过
            Unit existing = tile.Units.FirstOrDefault(x => x.UnitData.Id == unitId);
            if (existing == null)
                existing = tile.MidUnits.FirstOrDefault(x => x.UnitData.Id == unitId);

            if (existing != null)
            {
                if (setMod != "替换")
                    return;

                existing.Finish(true);
            }

            Operator = Battle.CreateSceneUnit(unitId, new Vector3(pos.x, 0, pos.y), direction, lifeTime) as 中立单位;
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