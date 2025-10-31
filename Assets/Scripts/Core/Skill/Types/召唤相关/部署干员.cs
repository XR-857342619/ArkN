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
    public class 部署干员 : Skill
    {
        public 干员 Operator;
        //public 干员 skilloprator;
        //public Vector2Int pos;
        public DirectionEnum direction;
        //public Vector3 pos = new Vector3(float.MaxValue, 0, float.MaxValue);
        public List<Vector3> posList;
        public string targetDirection;
        public string setMod;
        public string targetPos;
        public string name;
        public string unitId;
        public int mainSkillId;
        public int count;
        public override void Init()
        {
            base.Init();
            mainSkillId = SkillData.Data.GetInt("召唤物主技能索引", 0);
            targetPos = SkillData.Data.GetStr("召唤位置", "");
            targetDirection = SkillData.Data.GetStr("召唤物方向", "固定方向");
            setMod = SkillData.Data.GetStr("部署模式", "追加");
            unitId = SkillData.Data.GetStr("召唤物ID");
            name = SkillData.Data.GetStr("指定单位名称", "");
            count = SkillData.Data.GetInt("数量", 1);
            if (targetDirection == "固定方向")
                if (!Enum.TryParse(SkillData.Data.GetStr("方向"), out direction))
                    direction = DirectionEnum.Right;
        }
        public override void Start()
        {
            //base.Start();
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
            //Log.Debug("获取到技能来源:" + skilloprator.UnitData.Name);
            //if (SkillData.AttackPoints is not null)
            //    pos = SkillData.AttackPoints[0];
            //else
            //    pos = skilloprator.GridPos;
            
            for (int i = 0; i < posList.Count; i++)
            {
                if (i >= count) break;
                Unit nowOp = null;
                if (battleOp.Count > 0 && battleOp.Count < i)
                    nowOp = battleOp[i];
                GetToken(nowOp);
                SetToken(posList[i], direction, nowOp);
            }
        }
        public virtual List<Vector3> GetPosList()
        {
            FindTarget();
            //Debug.Log(Targets?.First()?.Position);
            switch (targetPos)
            {
                case "使用自身位置":
                    Debug.Log("useSelfPos:" + Unit.Position);
                    return new List<Vector3>() { Unit.Position };
                case "使用附加技能索敌位置":
                    if (SkillData.Skills.Count() > 0)
                    {
                        var skill = Unit.LearnSkill(SkillData.Skills[0]);
                        skill.Init();
                        List<Unit> targets = skill.GetAttackTarget();
                        return targets.Select(x => x.Position).ToList();
                    }
                    else
                    {
                        return new List<Vector3>();
                    }
                    //Debug.Log("useTargetPos:");
                    //break;
                case "使用干员攻击范围位置":
                    List<Vector3> AttackPoints2V3 = new List<Vector3>();
                    foreach (var point in AttackPoints)
                    {
                        AttackPoints2V3.Add(new Vector3(point.x, 0, point.y));
                    }
                    Debug.Log("useAttackPoint:");
                    return AttackPoints2V3;
                    //break;
                case "使用本技能索敌位置":
                    //Debug.Log(Targets.FirstOrDefault().Position);
                    return Targets.Select(x => x.Position).ToList();
                    //break;
            }
            return new List<Vector3>();
        }
        public virtual void GetToken(Unit battleOp = null)
        {
            if (battleOp is not null && setMod == "位移")
            {
                Operator = battleOp as 干员;
                Operator.NowGrid.Units.Remove(Operator);
            }
            else
            {
                Operator = Battle.CreatePlayerUnit(Database.Instance.GetIndex<UnitData>(unitId)) as 干员;
                Operator.Parent = Unit;
                Unit.Children.Add(Operator);
            }
            Operator.Parent = Unit;
        }
        public virtual void SetToken(Vector3 pos, DirectionEnum direction, Unit battleOp = null)
        {
            Debug.Log("获取到部署位置:" + pos + " 方向:" + direction);
            Tile tile = Battle.Map.Tiles[(int)pos.x, (int)pos.z];
            Unit toRemove = null;
            toRemove = tile.Units.Where(x => !x.UnitData.NotUseTile).FirstOrDefault();
            if (toRemove is not null && toRemove is Units.干员 toRemoveOprator)
            //if (toRemove is not null)
                toRemoveOprator.LeaveMap();
            if (tile.CanSet(Operator.UnitData))
            {
                Log.Debug("部署干员:" + Operator.UnitData.Name + "于" + pos);
                //Log.Debug(Operator.Skills.Count());
                //GameObject go = Operator.UnitModel.gameObject;
                //go.transform.position = new Vector3(pos.x, 0.5f, pos.z);
                if (Operator.UnitData.MainSkill is not null && Operator.UnitData.MainSkill.Count() >= 0 && Operator.MainSkill is null)
                    Operator.MainSkill = Operator.LearnSkill(Operator.UnitData.MainSkill[mainSkillId], null);
                Operator.ChangePos((int)pos.x, (int)pos.z, direction);
                Operator.JoinMap();
                //tile.Units.Add(Operator);
            }
            else
            {
                if (toRemove is not null && toRemove is Units.干员 RemovedOperator)
                    tile.Units.Add(RemovedOperator);
                if (battleOp is not null)
                    Operator.NowGrid.Units.Add(Operator);
                Log.Debug("无法部署干员:" + Operator.UnitData.Name + "于" + pos);
                return;
            }
        }
    }
}
