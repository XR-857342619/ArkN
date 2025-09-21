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
    public class 部署干员 : Skill
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
            mainSkillId = SkillData.Data.GetInt("MainSkillIndex", 0);
            targetPos = SkillData.Data.GetStr("TargetPos", "");
            targetDirection = SkillData.Data.GetStr("UseMod", "FixedDirection");
            setMod = SkillData.Data.GetStr("SetMod", "Add");
            if (targetDirection == "FixedDirection")
                Enum.TryParse(SkillData.Data.GetStr("Direction"), out direction);
        }
        public override void Start()
        {
            //base.Start();
            FindTarget();
            Debug.Log(Targets.First().Position);
            switch (targetPos)
            {
                case "useSelfPos":
                    Debug.Log("useSlefPos:" + Unit.Position);
                    pos = Unit.Position;
                    break;
                case "useTargetPos":
                    if (SkillData.Skills.Count() > 0)
                    {
                        var skill = Unit.LearnSkill(SkillData.Skills[0]);
                        skill.Init();
                        List<Unit> targets = skill.GetAttackTarget();
                        if (targets.Count > 0)
                        {
                            pos = targets.First().Position;
                        }
                    }
                    else
                    {
                        pos = Unit.Position;
                    }
                    Debug.Log("useTargetPos:");
                    break;
                case "useAttackPoint":
                    foreach (var point in AttackPoints)
                    {
                        if (point != Unit.Position2)
                        {
                            pos.x = point.x;
                            pos.z = point.y;
                        }
                    }
                    Debug.Log("useAttackPoint:");
                    break;
                case "useSelfTargetPos":
                    Debug.Log(Targets.First().Position);
                    pos = Targets.First().Position;
                    break;
            }
            string unitId = SkillData.Data.GetStr("UnitId");
            Unit battleOp = Battle.AllUnits.Find(x => x.UnitData.Id == unitId);
            if (battleOp is not null)
            {
                Operator = battleOp as 干员;
            }
            else
            {
                Operator = Battle.CreatePlayerUnit(Database.Instance.GetIndex<UnitData>(unitId)) as 干员;
            }
            if (targetDirection == "UserDirection")
            {
                name = SkillData.Data.GetStr("UserName", "");
                if (Battle.AllUnits.Find(x => x.UnitData.Name == name) is Units.干员 skilloprator)
                    direction = skilloprator.Direction_E;
            }
            //Log.Debug("获取到技能来源:" + skilloprator.UnitData.Name);
            //if (SkillData.AttackPoints is not null)
            //    pos = SkillData.AttackPoints[0];
            //else
            //    pos = skilloprator.GridPos;
            Debug.Log("获取到部署位置:" + pos + " 方向:" + direction);
            Tile tile = Battle.Map.Tiles[(int)pos.x, (int)pos.z];
            Units.干员 toRemove = null;
            foreach (Unit unit in tile.Units)
            {
                if (unit is Units.干员 oprator)
                {
                    if (!oprator.UnitData.NotUseTile && setMod == "Replace" && !Operator.UnitData.NotUseTile)
                    {
                        toRemove = oprator;
                        //tile.Units.Remove(oprator);
                    }
                    else
                        continue;
                }
                //tile.Units.Remove(skilloprator);
            }
            if (toRemove is not null)
                toRemove.LeaveMap();
            if (tile.CanSet(Operator, Operator.UnitData.NotUseTile))
            {
                Log.Debug("部署干员:" + Operator.UnitData.Name + "于" + pos);
                Log.Debug(Operator.Skills.Count());
                GameObject go = Operator.UnitModel.gameObject;
                go.transform.position = new Vector3(pos.x, 0.5f, pos.z);
                if (Operator.UnitData.MainSkill is not null && Operator.UnitData.MainSkill.Count() >= 0)
                    Operator.MainSkill = Operator.LearnSkill(Operator.UnitData.MainSkill[mainSkillId], null);
                Operator.ChangePos((int)pos.x, (int)pos.z, direction);
                Operator.JoinMap();
                Operator.Parent = Battle.AllUnits.Find(x => x.UnitData.Name == name) as Units.干员??null;
                tile.Units.Add(Operator);
            }
            else
            {
                if (toRemove is not null)
                    tile.Units.Add(toRemove);
                Log.Debug("无法部署干员:" + Operator.UnitData.Name + "于" + pos);
                return;
            }
        }
    }
}
