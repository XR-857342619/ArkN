using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Units;
using UnityEngine;

namespace Skills
{
    public class 召唤 : Skill
    {
        WaveInfo summonWaveInfo;
        float range;
        int count;

        string unitId;
        string targetPos;
        string setMod;

        public override void Init()
        {
            base.Init();

            unitId = SkillData.Data.GetStr("召唤物ID");
            if (string.IsNullOrEmpty(unitId))
                unitId = SkillData.Data.GetStr("UnitId");

            range = SkillData.Data.GetFloat("范围", SkillData.Data.GetFloat("Range", 0f));
            count = SkillData.Data.GetInt("数量", SkillData.Data.GetInt("Count", 1));
            targetPos = SkillData.Data.GetStr("召唤位置", "");
            setMod = SkillData.Data.GetStr("部署模式", "追加");

            if (setMod == "位移") count = 1;
            if (string.IsNullOrEmpty(unitId) && setMod != "位移") return;

            if (Unit is Units.敌人 parent && parent.WaveData != null)
            {
                summonWaveInfo = JsonHelper.Clone(parent.WaveData);
            }
            else
            {
                summonWaveInfo = new WaveInfo();
            }
            summonWaveInfo.sUnitId = unitId;
            Debug.Log($"技能 {SkillData.Id} 初始化召唤物 {unitId}，范围 {range}，数量 {count}，位置模式 {targetPos}，部署模式 {setMod}");
        }

        public override void SpSkillEffect()
        {
            var caster = Unit;
            Debug.Log($"技能 {SkillData.Id} 由 {caster?.UnitData.Id} 施放");
            if (caster == null || (string.IsNullOrEmpty(unitId) && setMod != "位移")) return;

            List<Vector2Int> posList = GetPosList(caster);
            
            if (posList.Count == 0)
            {
                Log.Debug(SkillData.Id + "无法获取到召唤位置");
                return;
            }
            Debug.Log($"获取到召唤位置: {string.Join(", ", posList)}");

            for (int i = 0; i < posList.Count; i++)
            {
                Debug.Log($"在位置 {posList[i].x}, {posList[i].y} 召唤 {count} 个单位");
                for (int j = 0; j < count; j++)
                {
                    Debug.Log($"召唤第 {j + 1} 个单位");
                    SpawnEnemy(caster, GetRandomPositions(posList[i]));
                }
            }
        }

        private List<Vector2Int> GetPosList(Unit caster)
        {
            FindTarget();

            switch (targetPos)
            {
                case "使用自身位置":
                    return new List<Vector2Int> { caster.GridPos };

                case "使用本技能索敌位置":
                    return Targets.Select(x => x.GridPos).ToList();

                case "使用附加技能索敌位置":
                    if (SkillData.Skills != null && SkillData.Skills.Length > 0)
                    {
                        var skill = caster.LearnSkill(SkillData.Skills[0]);
                        skill.Init();
                        return skill.GetAttackTarget().Select(x => x.GridPos).ToList();
                    }
                    return new List<Vector2Int>();

                case "使用干员攻击范围位置":
                    return AttackPoints != null ? new List<Vector2Int>(AttackPoints) : new List<Vector2Int>();

                default:
                    return new List<Vector2Int> { caster.GridPos };
            }
        }

        private Vector2 GetRandomPositions(Vector2Int pos)
        {
            float x = Battle.NextFloat(pos.x - range, pos.x + range);
            float y = Battle.NextFloat(pos.y - range, pos.y + range);
            return new Vector2(x, y);
        }

        private void SpawnEnemy(Unit caster, Vector2 pos)
        {
            //Tile tile = Battle.Map.Tiles[pos.x, pos.y];

            if (setMod == "替换")
            {
                Unit old = Battle.FindAll(pos.ToV2Int(), 2).FirstOrDefault(x => x is Units.敌人 && x.UnitData.Id == unitId);
                if (old is not null)
                {
                    old.Finish(true);
                }
            }
            else if (setMod == "位移")
            {
                if (caster is 敌人) caster.Position = pos;
                var V2Int = pos.ToV2Int();
                if (caster is 干员 op) op.ChangePos(V2Int.x, V2Int.y, op.Direction_E);
                return;
            }

            var unit = Battle.CreateEnemy(summonWaveInfo);
            if (unit == null) return;
            Debug.Log($"召唤单位 {unit.UnitData.Id} 到位置 {pos.x}, {pos.y}");

            unit.Position = new Vector3(pos.x, caster.Position.y, pos.y);
            if (caster is Units.敌人 parent)
            {
                unit.currentPathIndex = parent.currentPathIndex;
                unit.Parent = parent;
            }
        }
    }
}