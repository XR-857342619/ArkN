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
        public override void Start()
        {
            //base.Start();
            //Debug.Log(SkillData.Id + " 部署装置开始");
            posList = GetPosList();
            if (posList.Count == 0)
            {
                Log.Debug("无法获取到部署位置");
                return;
            }
            List<Unit> battleOp = Battle.AllUnits.FindAll(x => x.UnitData.Id == unitId);

            if (targetDirection == "使用指定单位方向")
            {
                if (Battle.AllUnits.Find(x => x.UnitData.Name == name) is Units.干员 skilloprator)
                    direction = skilloprator.Direction_E;
            }
            directionV2 = DirectionHelper.DirectionToInt(direction);
            //Log.Debug("获取到技能来源:" + skilloprator.UnitData.Name);
            //if (SkillData.AttackPoints is not null)
            //    pos = SkillData.AttackPoints[0];
            //else
            //    pos = skilloprator.GridPos;
            
            for (int i = 0; i < posList.Count; i++)
            {
                if (i >= count) break;

                SetToken(posList[i], directionV2, lifeTime);
            }
        }
        public virtual void SetToken(Vector2Int pos, Vector2 direction, float lifeTime = 0)
        {
            //Debug.Log("部署装置：" + unitId + " 到：" + pos + " 方向：" + direction + " 持续：" + lifeTime);
            if (Battle.Map.Tiles[pos.x, pos.y].Units.Any(x => x.UnitData.Id == unitId)) return;
            if (Battle.Map.Tiles[pos.x, pos.y].MidUnits.Any(x => x.UnitData.Id == unitId)) return;
            Operator = Battle.CreateSceneUnit(unitId, new Vector3(pos.x, 0, pos.y), direction, lifeTime) as 中立单位;
            Operator.Parent = Unit;
            Unit.Children.Add(Operator);
        }
    }
}
