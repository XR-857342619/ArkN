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
    public class 位移 : 部署干员
    {
        //public new 干员 Operator;
        public override void Start()
        {
            //base.Start();
            posList = GetPosList();
            if (posList.Count == 0)
            {
                Log.Debug("无法获取到部署位置");
                return;
            }

            if (Battle.AllUnits.Find(x => x.UnitData.Name == name) is Units.干员 skilloprator)
                direction = skilloprator.Direction_E;

            GetToken();
            SetToken(posList.FirstOrDefault(), direction);
        }

        public virtual void GetToken()
        {
            if (Unit is 干员 op)
                Operator = op;
            else
                Operator = null;
        }
        public virtual void SetToken(Vector2Int pos, DirectionEnum direction)
        {
            if (Operator is null) return;
            Debug.Log("获取到部署位置:" + pos + " 方向:" + direction);
            Tile orgTile = Battle.Map.Tiles[Operator.GridPos.x, Operator.GridPos.y];
            orgTile.Units.Remove(Operator);
            
            Tile tile = Battle.Map.Tiles[pos.x, pos.y];
            Operator.ChangePos(pos.x, pos.y, direction);
            Operator.JoinMap();
        }
    }
}
