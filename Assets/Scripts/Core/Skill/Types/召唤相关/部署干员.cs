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
        public List<Vector2Int> posList;
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
        public override void SpSkillEffect()
        {
            posList = GetPosList();
            if (posList.Count == 0)
            {
                Log.Debug(this.SkillData.Id + "无法获取到部署位置");
                return;
            }
            List<Unit> battleOp = Battle.AllUnits.FindAll(x => x.UnitData.Id == unitId && x.InputTime >= 0);

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
                if (battleOp.Count > i)
                    nowOp = battleOp[i];
                GetToken(nowOp);
                if (Operator is not null) SetToken(posList[i], direction, nowOp);
                //foreach (Unit unit in Unit.Battle.AllUnits) Debug.Log(unit.UnitData.Name);
            }
        }
        public virtual List<Vector2Int> GetPosList()
        {
            FindTarget();
            //Debug.Log(this.SkillData.Id);
            //Debug.Log(Targets?.First()?.Position);
            switch (targetPos)
            {
                case "使用自身位置":
                    //Debug.Log("useSelfPos:" + Unit.Position);
                    return new List<Vector2Int>() { Unit.GridPos };
                case "使用附加技能索敌位置":
                    if (SkillData.Skills is not null && SkillData.Skills.Count() > 0)
                    {
                        var skill = Unit.LearnSkill(SkillData.Skills[0]);
                        skill.Init();
                        List<Unit> targets = skill.GetAttackTarget();
                        return targets.Select(x => x.GridPos).ToList();
                    }
                    else
                    {
                        return new List<Vector2Int>();
                    }
                    //Debug.Log("useTargetPos:");
                    //break;
                case "使用干员攻击范围位置":
                    //List<Vector3> AttackPoints2V3 = new List<Vector3>();
                    //foreach (var point in AttackPoints)
                    //{
                    //    AttackPoints2V3.Add(new Vector3(point.x, 0, point.y));
                    //}
                    Debug.Log("useAttackPoint:");
                    return AttackPoints ?? new List<Vector2Int>();
                    //break;
                case "使用本技能索敌位置":
                    //Debug.Log(Targets.FirstOrDefault().Position);
                    return Targets.Select(x => x.GridPos).ToList();
                    //break;
            }
            return new List<Vector2Int>();
        }
        public virtual void GetToken(Unit battleOp = null)
        {
            if (battleOp is Units.干员 op && setMod == "位移")
            {
                Operator = op;
                if (Operator.NowGrid != null)
                    Operator.NowGrid.Units.Remove(Operator);
            }
            else
            {
                Operator = Battle.CreatePlayerUnit(Database.Instance.GetIndex<UnitData>(unitId)) as 干员;
            }

            if (Operator == null)
            {
                Log.Debug("创建干员失败");
                return;
            }

            Operator.Parent = Unit;

            if (!Unit.Children.Contains(Operator))
                Unit.Children.Add(Operator);
        }
        public virtual void SetToken(Vector2Int pos, DirectionEnum direction, Unit battleOp = null)
        {
            //Debug.Log("获取到部署位置:" + pos + " 方向:" + direction);
            Tile tile = Battle.Map.Tiles[pos.x, pos.y];
            Unit toRemove = null;
            干员 toRemoveOp = null;
            toRemove = tile.Units.Where(x => !x.UnitData.NotUseTile).FirstOrDefault();
            if (toRemove is Units.干员 toRemoveOprator && (setMod == "替换"))
            {
                tile.Units.Remove(toRemoveOprator);
                toRemoveOp = toRemoveOprator;
            }

            if (setMod == "位移") Battle.Map.Tiles[Unit.GridPos.x, Unit.GridPos.y].Units.Remove(Unit);

            if (tile.CanSet(Operator.UnitData))
            {
                Log.Debug("部署干员:" + Operator.UnitData.Name + "于" + pos);

                if (setMod == "替换" && toRemoveOp is not null) toRemoveOp.LeaveMap(noEvent: true);

                if (Operator.UnitData.MainSkill is not null && Operator.UnitData.MainSkill.Count() >= 0 && Operator.MainSkill is null)
                {
                    int skillIndex = Mathf.Clamp(mainSkillId, 0, Operator.UnitData.MainSkill.Length - 1);
                    Operator.MainSkill = Operator.LearnSkill(Operator.UnitData.MainSkill[skillIndex], null);
                }
                Operator.ChangePos(pos.x, pos.y, direction);
                Operator.JoinMap(true);
                //tile.Units.Add(Operator);
            }
            else
            {
                if (setMod == "替换" && toRemoveOp is not null)
                {
                    tile.Units.Add(toRemoveOp);
                }
                
                if (setMod == "位移") Battle.Map.Tiles[Unit.GridPos.x, Unit.GridPos.y].Units.Add(Operator);

                Log.Debug("无法部署干员:" + Operator.UnitData.Name + "于" + pos);
                return;
            }
        }
    }
}
