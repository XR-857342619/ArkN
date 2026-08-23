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
            if (string.IsNullOrEmpty(unitId)) return;

            range = SkillData.Data.GetFloat("范围", SkillData.Data.GetFloat("Range", 0f));
            count = SkillData.Data.GetInt("数量", SkillData.Data.GetInt("Count", 1));
            targetPos = SkillData.Data.GetStr("召唤位置", "");
            setMod = SkillData.Data.GetStr("部署模式", "追加");

            if (Unit is Units.敌人 parent && parent.WaveData != null)
            {
                summonWaveInfo = JsonHelper.Clone(parent.WaveData);
            }
            else
            {
                summonWaveInfo = new WaveInfo();
            }
            summonWaveInfo.sUnitId = unitId;
        }

        public override void Effect(Unit target)
        {
            base.Effect(target);

            var caster = Unit;
            if (caster == null || string.IsNullOrEmpty(unitId)) return;

            List<Vector2Int> posList = GetPosList(caster);
            
            if (posList.Count == 0)
            {
                Log.Debug(SkillData.Id + "无法获取到召唤位置");
                return;
            }
            Debug.Log($"获取到召唤位置: {string.Join(", ", posList)}");

            for (int i = 0; i < posList.Count; i++)
            {
                if (i >= count) break;
                SpawnEnemy(caster, posList[i]);
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
                    return GetRandomPositions(caster);
            }
        }

        private List<Vector2Int> GetRandomPositions(Unit caster)
        {
            var result = new List<Vector2Int>();
            for (int i = 0; i < 4; i++)
            {
                float x = Battle.NextFloat(caster.GridPos.x - range, caster.GridPos.x + range);
                float y = Battle.NextFloat(caster.GridPos.y - range, caster.GridPos.y + range);
                result.Add(new Vector2Int(Mathf.RoundToInt(x), Mathf.RoundToInt(y)));
            }
            return result;
        }

        private void SpawnEnemy(Unit caster, Vector2Int pos)
        {
            Tile tile = Battle.Map.Tiles[pos.x, pos.y];

            if (setMod == "替换")
            {
                Unit old = tile.Units.FirstOrDefault(x => x is Units.敌人 && x.UnitData.Id == unitId);
                if (old != null)
                {
                    old.Finish(true);
                }
            }
            else if (setMod == "位移")
            {
                var enemyCaster = caster as Units.敌人;
                Unit existing;
                if (enemyCaster != null)
                {
                    existing = Battle.AllUnits.FirstOrDefault(x => x.UnitData.Id == unitId && x.Parent == enemyCaster);
                }
                else
                {
                    existing = Battle.AllUnits.FirstOrDefault(x => x.UnitData.Id == unitId && x is Units.敌人 e && string.IsNullOrEmpty(e.WaveData?.Path));
                }

                if (existing is Units.敌人 existingEnemy)
                {
                    existingEnemy.Position = new Vector3(pos.x, existingEnemy.Position.y, pos.y);
                    existingEnemy.NeedResetPath = true;
                    return;
                }
            }

            var unit = Battle.CreateEnemy(summonWaveInfo);
            if (unit == null) return;
            Debug.Log($"召唤单位 {unit.UnitData.Id} 到位置 {pos.x}, {pos.y}");

            unit.Position = new Vector3(pos.x, tile.Pos.y, pos.y);
            if (caster is Units.敌人 parent)
            {
                unit.currentPathIndex = parent.currentPathIndex;
                unit.Parent = parent;
            }
        }
    }
}