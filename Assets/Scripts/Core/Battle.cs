using Buffs;
using Bullets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

public class Battle
{
    public Stack<TriggerData> TriggerDatas = new Stack<TriggerData>();
    public int Hp = 10;
    public int Hurt;
    public int Tick = -1;
    public Dictionary<string, int> WaveTags = new Dictionary<string, int>() {};
    public float Cost;
    //public float tmpCost;

    public Map Map = new Map();

    //public MapData MapData;
    public MapInfo MapData;

    public List<OneWave> Waves = new List<OneWave>();
    public List<OneWave> CheckPointWaves = new List<OneWave>();

    public List<MapUnitInfo> SceneUnits = new List<MapUnitInfo>();

    public int EnemyCount;

    public Unit RuleUnit;

    public List<Units.干员> PlayerUnits = new List<Units.干员>();
    public List<Unit> PlayerUnits2 = new List<Unit>();

    public List<Unit> Enemys = new List<Unit>();

    public List<Unit> AllUnits = new List<Unit>();

    public CountDown CostCounting = new CountDown(1);

    public float CostCountSpeed = 1;

    public bool Finish;

    public bool Win;

    // 按团队位掩码分组的空间索引：key = 1 << unit.Team（1=玩家, 2=敌人, 4=中立）
    public Dictionary<int, HashSet<Unit>[,]> teamMaps = new Dictionary<int, HashSet<Unit>[,]>();

    private float maxUnitRadius = 1f; // 用于九宫格索敌时计算搜索范围

    // 兼容旧调用方：返回敌人空间索引
    public HashSet<Unit>[,] UnitMap
    {
        get
        {
            if (teamMaps != null && teamMaps.TryGetValue(2, out var map)) return map;
            return null;
        }
    }

    public HashSet<Bullet> Bullets = new HashSet<Bullet>();

    public System.Random Random;

    public int BuildCount;

    public int TeamLimit = 99;
    public HashSet<UnitTypeEnum> ProfessionLimit = new HashSet<UnitTypeEnum>();
    public List<int> CheckPoints = new List<int>();

    public void Init(BattleInput battleConfig)
    {
        MapData = Database.Instance.GetMap(battleConfig.MapPackage, battleConfig.MapName);
        Hp = MapData.InitHp;
        int Seed = battleConfig.Seed != 0? battleConfig.Seed : (int)DateTime.Now.Ticks;
        Random = new System.Random(Seed);

        RuleUnit = new Unit();
        RuleUnit.Battle = this;
        RuleUnit.Init();
        //RuleUnit.LearnSkill(0, null);//神经损伤
        foreach (var contracrId in battleConfig.Contracts)
        {
            var contract = Database.Instance.Get<ContractData>(contracrId);
            if (contract.MapHp != 0) Hp = contract.MapHp;
            if (contract.TeamLimit > 0 && TeamLimit > contract.TeamLimit) TeamLimit = contract.TeamLimit;
            if (contract.ProfessionLimit != null) foreach (var p in contract.ProfessionLimit) ProfessionLimit.Add(p);
            var skills = contract.Skills;
            if (skills != null)
                foreach (var skillId in skills)
                {
                    RuleUnit.LearnSkill(skillId, null);
                }
        }

        Cost = MapData.InitCost;
        //tmpCost = MapData.InitCost;

        //读取场景地图信息
        Map.Init(this);
        //读取中立单位信息
        foreach (var u in MapData.UnitInfos)
        {
            try
            {
                Database.Instance.Get<UnitData>(u.UnitId);
            }
            catch (Exception e)
            {
                TipManager.Instance.ShowTip("地图单位数据错误：" + u.UnitId);
                Debug.LogError(e);
                continue;
            }
            SceneUnits.Add(new MapUnitInfo()
            {
                Time = u.ActiveTime,
                Id = u.UnitId,
                Tag = u.Tag,
                Pos = Map.Tiles[u.X, u.Y].MapGrid.transform.position,
                Direction = u.Direction,
                LifeTime = u.LifeTime,
            });
        }

        if (battleConfig.Team != null)
        {
            for (int i = 0; i < battleConfig.Team.Cards.Count; i++)
            {
                if (i >= TeamLimit) continue;
                Card unitInput = battleConfig.Team.Cards[i];
                if (ProfessionLimit.Contains(unitInput.UnitData.Profession)) continue;
                CreatePlayerUnit(unitInput, i >= battleConfig.Team.UnitSkill.Count ? 0 : battleConfig.Team.UnitSkill[i]);
            }
            BuildCount = MapData.MaxBuildCount;
        }
        //else
        //{
        //    foreach (var card in battleConfig.Dungeon.Cards)
        //    {
        //        CreatePlayerUnit(card, card.UsingSkill);
        //    }
        //    foreach (var relic in battleConfig.Dungeon.Relics)
        //    {
        //        var skills = relic.RelicData.Skills;
        //        if (skills != null)
        //            foreach (var skillId in skills)
        //            {
        //                RuleUnit.LearnSkill(skillId, null);
        //            }
        //    }
        //    BuildCount = battleConfig.Dungeon.MaxBuildCount;
        //}

        for (int i = 0; i < MapData.BoxCount; i++)
        {
            CreatePlayerUnit(Database.Instance.GetIndex<UnitData>("箱子"));
        }

        Trigger(TriggerEnum.起始);
        foreach (var unit in PlayerUnits)
        {
            TriggerDatas.Push(new TriggerData()
            {
                Target = unit,
            });
            //Debug.Log(unit.UnitData.Name + "trigger 出场");
            Trigger(TriggerEnum.出场);
            TriggerDatas.Pop();
        }

        teamMaps = new Dictionary<int, HashSet<Unit>[,]>();
        int mapWidth = Map.Tiles.GetLength(0);
        int mapHeight = Map.Tiles.GetLength(1);
        foreach (var key in new[] { 1, 2, 4 })
        {
            var map = new HashSet<Unit>[mapWidth, mapHeight];
            for (int i = 0; i < mapWidth; i++)
                for (int j = 0; j < mapHeight; j++)
                {
                    map[i, j] = new HashSet<Unit>();
                }
            teamMaps[key] = map;
        }

        //WaveData[] array = Database.Instance.GetAll<WaveData>();
        //for (int id = 0; id < array.Length; id++)
        foreach (var wave in MapData.WaveInfos)
        {
            //WaveData wave = array[id];
            for (int i = 0; i < wave.Count; i++)
            {
                if (!string.IsNullOrEmpty(wave.Tag)) continue;
                if (wave.sUnitId != null) EnemyCount++;
                var waveInfo = new OneWave() { WaveData = wave, Time = wave.Delay + wave.GapTime * i };
                if (wave.CheckPoint == 0)
                    Waves.Add(waveInfo);
                else
                    CheckPointWaves.Add(waveInfo);
            }
        }
        //SortWave();
        Waves.Sort((x, y) => Math.Sign(x.Time - y.Time));

        checkSceneUnit();

        //这里刷新下单位状态，有些开场附加数据需要刷新
        foreach (var unit in PlayerUnits)
        {
            unit.Refresh();
        }
        BattleManager.Instance.OpDamageInfos.Clear();
        if (battleConfig.Team != null)
        {
            foreach (var unit in battleConfig.Team.Cards)
            {
                OpDamageInfo opDamageInfo = new OpDamageInfo();
                opDamageInfo.UnitId = unit.UnitId;
                BattleManager.Instance.OpDamageInfos.Add(opDamageInfo);
            //Debug.Log(unit.UnitId);
            }
        }
    }
    public void Update()
    {
        updateFinish();
        if (Finish) return;
        Tick++;
        checkSceneUnit();
        updateUnitMap();
        if (SystemConfig.DeltaTime * CostCountSpeed > CostCounting.value)
        {
            Cost++;
            //tmpCost++;
            CostCounting.Set(1);
        }
        CostCounting.Update(SystemConfig.DeltaTime * CostCountSpeed);
        if (Cost > MapData.MaxCost) Cost = MapData.MaxCost;
        //if (BattleManager.Instance.IsInfCost) Cost = float.MaxValue/2;
        //else Cost = tmpCost;
        while (Waves.Count > 0 && Tick * SystemConfig.DeltaTime > Waves[0].Time)
        {
            var wave = Waves[0];
            Waves.RemoveAt(0);
            createWave(wave);
        }
        for (int i = CheckPointWaves.Count-1; i >= 0; i--)
        {
            OneWave checkPointWave = CheckPointWaves[i];
            if (CheckPoints.Count >= checkPointWave.WaveData.CheckPoint &&
                (Tick - CheckPoints[checkPointWave.WaveData.CheckPoint - 1]) * SystemConfig.DeltaTime > checkPointWave.Time)
            {
                CheckPointWaves.RemoveAt(i);
                createWave(checkPointWave);
            }
        }

        foreach (var tile in Map.Tiles)
        {
            tile?.Update();
        }

        foreach (var bullet in Bullets.ToArray())
        {
            bullet.Update();
        }

        foreach (var unit in AllUnits.ToArray())
        {
            unit.UpdatePush();
        }
        foreach (var unit in AllUnits.ToArray())
        {
            unit.UpdateBuffs();
        }
        foreach (var unit in AllUnits.ToArray())
        {
            unit.UpdateAction();
        }
        foreach (var unit in Enemys)
        {
            unit.UpdateCollision();
        }
        foreach (var unit in Enemys.ToArray())
        {
            if (unit.State==StateEnum.Die&& unit.Dying.Finished())
            {
                Enemys.Remove(unit);
                AllUnits.Remove(unit);
                unit.Finish();
            }
        }
    }

    void checkSceneUnit()
    {
        for (int i = SceneUnits.Count - 1; i >= 0; i--)
        {
            var unit = SceneUnits[i];
            if (string.IsNullOrEmpty(SceneUnits[i].Tag)||WaveTags.ContainsKey(unit.Tag))
            {
                float startTick = 0;
                if (!string.IsNullOrEmpty(SceneUnits[i].Tag)) startTick = WaveTags[unit.Tag];
                if (unit.Time <= (Tick - startTick) * SystemConfig.DeltaTime)
                {
                    CreateSceneUnit(unit.Id, unit.Pos, unit.Direction, unit.LifeTime);
                    SceneUnits.RemoveAt(i);
                }
            }
        }
    }

    void createWave(OneWave wave)
    {
        if (wave.WaveData.sUnitId == null)
        {
            var pathInfo = MapData.PathInfos.Find(x => x.Name == wave.WaveData.Path);
            //var PathPoints = pathInfo.Path;
            //List<Vector3> p = new List<Vector3>();
            //for (int i = 0; i < PathPoints.Count - 1; i++)
            //{
            //    var p1 = Map.FindPath(PathPoints[i].Pos, PathPoints[i + 1].Pos, PathPoints[i].DirectMove);
            //    p.AddRange(p1);
            //}
            TrailManager.Instance.ShowPath(AStarPathFinder.FindPath(Map.Tiles, pathInfo.Path.Select(x => x.Pos).ToList(), pathInfo.FlyPath));
        }
        else
        {
            var enemy = CreateEnemy(wave.WaveData);
            if (enemy == null) return;
            TriggerDatas.Push(new TriggerData()
            {
                Target = enemy,
            });
            Trigger(TriggerEnum.入场);
            enemy.Trigger(TriggerEnum.自己入场);
            TriggerDatas.Pop();
        }
    }

    public void SortSceneUnit()
    {
        //Waves = Waves.OrderBy(x => WaveTags.ContainsKey(x.) ? (x.Time + (WaveTags[x.Tag] - Tick) * SystemConfig.DeltaTime) : float.MaxValue).ToList();
        SceneUnits = SceneUnits.OrderBy(x => WaveTags.ContainsKey(x.Tag) ? (x.Time + (WaveTags[x.Tag] - Tick) * SystemConfig.DeltaTime) : float.MaxValue).ToList();
    }

    public void ChangeWaveTag(string tag)
    {
        foreach (var wave in MapData.WaveInfos)
        {
            if (wave.Tag != tag) continue;
            if (Enemys.Any(x => (x is Units.敌人 u) && u.WaveData == wave)) continue;
            for (int i = 0; i < wave.Count; i++)
            {
                if (wave.sUnitId != null) EnemyCount++;
                var waveInfo = new OneWave() { WaveData = wave, Time = (Tick + 1) * SystemConfig.DeltaTime + wave.Delay + wave.GapTime * i };
                if (wave.CheckPoint == 0)
                    Waves.Add(waveInfo);
                else
                    CheckPointWaves.Add(waveInfo);
            }
        }
        Waves.Sort((x, y) => Math.Sign(x.Time - y.Time));
        if (!WaveTags.ContainsKey(tag)) WaveTags.Add(tag, Tick);
        else WaveTags[tag] = Tick;
        SortSceneUnit();
    }

    public Unit CreateSceneUnit(string id,Vector3 pos,Vector2 direction,float lifeTime)
    {
        var unitData = Database.Instance.Get<UnitData>(id);
        if (unitData ==null) return null;
        var unit = typeof(Battle).Assembly.CreateInstance(nameof(Units) + "." + unitData.Type) as Unit;
        unit.Id = Database.Instance.GetIndex(unitData);
        if (unit.Id == 0) return null;
        unit.Battle = this;
        unit.Position = pos;
        unit.Direction = direction;
        unit.Init();
        if (lifeTime != 0) unit.LifeTime = new CountDown(lifeTime);
        if (!unit.UnitData.NotUseTile)
            Map.Tiles[(int)pos.x, (int)pos.z].Units.Add(unit);
        else
            Map.Tiles[(int)pos.x, (int)pos.z].MidUnits.Add(unit);
        AllUnits.Add(unit);
        if (unit.Team == 0) PlayerUnits2.Add(unit);
        return unit;
    }

    //public Unit CreateTempUnit(Vector3 pos, Vector2 direction)
    //{
    //    //var unitData = Database.Instance.Get<UnitData>(id);
    //    //if (unitData == null) return null;
    //    var unit = typeof(Battle).Assembly.CreateInstance(nameof(Units) + ".普通单位") as Unit;
    //    //unit.Id = Database.Instance.GetIndex(unitData);
    //    unit.Battle = this;
    //    unit.Position = pos;
    //    unit.Direction = direction;
    //    unit.Init();
    //    //if (lifeTime != 0) unit._lifeTime = new CountDown(lifeTime);
    //    //if (!unit.UnitData.NotUseTile)
    //    //    Map.Tiles[(int)pos.x, (int)pos.z].Units.Add(unit);
    //    //else
    //    //    Map.Tiles[(int)pos.x, (int)pos.z].MidUnit = unit;
    //    AllUnits.Add(unit);
    //    //if (unit.Team == 0) PlayerUnits2.Add(unit);
    //    return unit;
    //}

    public Units.干员 CreatePlayerUnit(ICard card,int skill)
    {
        var config = Database.Instance.Get<UnitData>(card.UnitId);
        var unit = typeof(Battle).Assembly.CreateInstance(nameof(Units) + "." + config.Type) as Units.干员;
        //unit.dircectAssetAsset = ResHelper.GetAsset<GameObject>(PathHelper.OtherPath + "ShowDirection");
        unit.Id = Database.Instance.GetIndex<UnitData>(config);
        if (unit.Id == 0) return null;
        unit.Card = card;
        unit.MainSkillId = skill;
        //unit.SetDirection(direction);
        unit.Battle = this;
        unit.Init();
        //var grid = Map.Grids[x, y];
        //unit.Position = grid.transform.position + new Vector3(0, config.Height, 0);
        PlayerUnits.Add(unit);
        AllUnits.Add(unit);
        return unit;
    }

    public Units.干员 CreatePlayerUnit(int id, int skill = 0)
    {
        var config = Database.Instance.Get<UnitData>(id);
        var unit = typeof(Battle).Assembly.CreateInstance(nameof(Units) + "." + config.Type) as Units.干员;
        unit.Id = id;
        unit.MainSkillId = skill;
        //unit.SetDirection(direction);
        unit.Battle = this;
        unit.Init();
        //var grid = Map.Grids[x, y];
        //unit.Position = grid.transform.position + new Vector3(0, config.Height, 0);
        PlayerUnits.Add(unit);
        AllUnits.Add(unit);
        return unit;
    }

    public Units.敌人 CreateEnemy(WaveInfo waveConfig)
    {
        var config = Database.Instance.Get<UnitData>(waveConfig.sUnitId);
        if (config == null)
        {
            config = Database.Instance.Get<UnitData>("enemy_1106_byokai");
            waveConfig.sUnitId = "enemy_1106_byokai";
        }
        var unit = typeof(Battle).Assembly.CreateInstance(nameof(Units) + "." + config.Type) as Units.敌人;
        unit.Id = Database.Instance.GetIndex<UnitData>(waveConfig.sUnitId);
        if (unit.Id == 0) return null;
        unit.WaveData = waveConfig;
        unit.Battle = this;
        unit.Init();
        //var grid = Map.Grids[waveConfig.Path, y];
        //unit.Position = grid.transform.position + new Vector3(0, config.Height, 0);
        Enemys.Add(unit);
        unit.index = Enemys.IndexOf(unit);
        if (unit.Team == 0) PlayerUnits2.Add(unit);
        AllUnits.Add(unit);
        TriggerDatas.Push(new TriggerData()
        {
            Target = unit,
        });
        Trigger(TriggerEnum.出场);
        TriggerDatas.Pop();
        return unit;
    }

    public Bullet CreateBullet(int id, Vector3 startPos, Vector3 targetPos, Unit target, Skill skill)
    {
        var config = Database.Instance.Get<BulletData>(id);
        var result = typeof(Battle).Assembly.CreateInstance(nameof(Bullets) + "." + config.Type) as Bullet;
        result.Id = id;
        result.Position = startPos;
        result.TargetPos = targetPos;
        result.Target = target;
        result.Skill = skill;
        Bullets.Add(result);
        result.Init();
        //Debug.Log("创建子弹");
        //Debug.Log(result.Id + " "+result.Position+" "+result.TargetPos+" "+result.Target+" "+result.Skill);
        return result;
    }
    public Bullet CreateBullet(int id, Vector3 startPos, Vector3 targetPos, Unit target, float specialValue, Skill skill)
    {
        var config = Database.Instance.Get<BulletData>(id);
        var result = typeof(Battle).Assembly.CreateInstance(nameof(Bullets) + "." + config.Type) as Bullet;
        result.Id = id;
        result.Position = startPos;
        result.TargetPos = targetPos;
        result.Target = target;
        result.Skill = skill;
        Bullets.Add(result);
        result.Init();
        
        return result;
    }

    public HashSet<Unit> FindAll(Vector2Int point,int team,bool aliveOnly=true)
    {
        var result = new HashSet<Unit>();
        if (Map.Tiles.GetLength(0) <= point.x || Map.Tiles.GetLength(1) <= point.y || point.x < 0 || point.y < 0) return result;

        if ((team & 1) != 0 && teamMaps.TryGetValue(1, out var playerMap))
        {
            Unit target = null;
            foreach (var unit in playerMap[point.x, point.y])
            {
                if ((!aliveOnly || unit.Alive()) && (team & (1 << unit.Team)) != 0)
                {
                    if (target == null
                        || (unit.UnitData.NotUseTile == target.UnitData.NotUseTile && unit.InputTime > target.InputTime)
                        || (!unit.UnitData.NotUseTile && target.UnitData.NotUseTile)
                        )
                        target = unit;
                }
            }
            if (target != null)
                result.Add(target);
        }
        if ((team & 2) != 0 && teamMaps.TryGetValue(2, out var enemyMap))
        {
            foreach (var unit in enemyMap[point.x, point.y])
            {
                if ((!aliveOnly || unit.Alive()) && (team & (1 << unit.Team)) != 0)
                    result.Add(unit);
            }
        }
        if ((team & 4) != 0 && teamMaps.TryGetValue(4, out var neutralMap))
        {
            foreach (var unit in neutralMap[point.x, point.y])
            {
                if ((!aliveOnly || unit.Alive()) && (team & (1 << unit.Team)) != 0)
                    result.Add(unit);
            }
        }
        return result;
    }

    public HashSet<Unit> FindAll(List<Vector2Int> points, int team, bool aliveOnly = true)
    {
        var result = new HashSet<Unit>();
        foreach (var point in points)
        {
            if (point.x < 0 || point.y < 0 || point.x >= Map.Tiles.GetLength(0) || point.y >= Map.Tiles.GetLength(1)) continue;

            if ((team & 1) != 0 && teamMaps.TryGetValue(1, out var playerMap))
            {
                foreach (var unit in playerMap[point.x, point.y])
                {
                    if ((!aliveOnly || unit.Alive()) && (team & (1 << unit.Team)) != 0)
                        result.Add(unit);
                }
            }
            if ((team & 2) != 0 && teamMaps.TryGetValue(2, out var enemyMap))
            {
                foreach (var unit in enemyMap[point.x, point.y])
                {
                    if ((!aliveOnly || unit.Alive()) && (team & (1 << unit.Team)) != 0)
                        result.Add(unit);
                }
            }
            if ((team & 4) != 0 && teamMaps.TryGetValue(4, out var neutralMap))
            {
                foreach (var unit in neutralMap[point.x, point.y])
                {
                    if ((!aliveOnly || unit.Alive()) && (team & (1 << unit.Team)) != 0)
                        result.Add(unit);
                }
            }
        }
        return result;
    }

    public HashSet<Unit> FindAll(Vector2 pos, float radius, int team, bool aliveOnly = true)
    {
        HashSet<Unit> result = new HashSet<Unit>();
        if (radius <= 0f) return result;

        int mapWidth = Map.Tiles.GetLength(0);
        int mapHeight = Map.Tiles.GetLength(1);

        // 九宫格范围：按半径 + 最大单位半径扩大搜索格范围
        float searchRadius = radius + maxUnitRadius;
        int minX = Mathf.FloorToInt(pos.x - searchRadius);
        int maxX = Mathf.CeilToInt(pos.x + searchRadius);
        int minY = Mathf.FloorToInt(pos.y - searchRadius);
        int maxY = Mathf.CeilToInt(pos.y + searchRadius);

        foreach (var kv in teamMaps)
        {
            if ((team & kv.Key) == 0) continue;
            var map = kv.Value;

            for (int x = minX; x <= maxX; x++)
            {
                for (int y = minY; y <= maxY; y++)
                {
                    if (x < 0 || y < 0 || x >= mapWidth || y >= mapHeight) continue;
                    foreach (var unit in map[x, y])
                    {
                        if ((!aliveOnly || unit.Alive()) && (team & (1 << unit.Team)) != 0)
                        {
                            float unitRange = radius + unit.UnitData.Radius;
                            if ((unit.Position2 - pos).sqrMagnitude < unitRange * unitRange)
                                result.Add(unit);
                        }
                    }
                }
            }
        }
        return result;
    }

    public HashSet<Bullet> FindAllBullets(Vector2 pos)
    {
        HashSet<Bullet> result = new HashSet<Bullet>();
        foreach (Bullet bullet in Bullets)
        {
            if (bullet.Position.x < pos.x+0.5 && bullet.Position.x > pos.x-0.5 && bullet.Position.z < pos.y+0.5 && bullet.Position.z > pos.y-0.5)
                result.Add(bullet);
        }
        return result;
    }

    public HashSet<Bullet> FindAllBullets(Vector3 pos, float radius)
    {
        HashSet<Bullet> result = new HashSet<Bullet>();
        foreach (var bullet in Bullets)
        {
            if ((bullet.Position - pos).magnitude < radius)
                result.Add(bullet);
        }
        return result;
    }

    void updateUnitMap()
    {
        // 清空全部团队空间索引
        foreach (var kv in teamMaps)
        {
            foreach (var tile in kv.Value)
            {
                tile.Clear();
            }
        }

        maxUnitRadius = 1f;
        // 统一遍历 AllUnits，按 Team 写入对应团队索引
        foreach (var unit in AllUnits)
        {
            if (unit.UnitData.Radius > maxUnitRadius)
                maxUnitRadius = unit.UnitData.Radius;

            int teamKey = 1 << unit.Team;
            if (!teamMaps.TryGetValue(teamKey, out var map)) continue;

            int width = map.GetLength(0);
            int height = map.GetLength(1);

            if (unit.UnitData.Size == Vector2Int.zero)
            {
                for (int i = Mathf.RoundToInt(unit.Position2.x - unit.UnitData.Radius); i <= Mathf.RoundToInt(unit.Position2.x + unit.UnitData.Radius); i++)
                {
                    for (int j = Mathf.RoundToInt(unit.Position2.y - unit.UnitData.Radius); j <= Mathf.RoundToInt(unit.Position2.y + unit.UnitData.Radius); j++)
                    {
                        if (i >= 0 && i < width && j >= 0 && j < height)
                            map[i, j].Add(unit);
                    }
                }
            }
            else
            {
                for (int i = Mathf.RoundToInt(unit.Position2.x - unit.UnitData.Size.x / 2); i < Mathf.RoundToInt(unit.Position2.x + (unit.UnitData.Size.x + 1) / 2); i++)
                {
                    for (int j = Mathf.RoundToInt(unit.Position2.y); j < Mathf.RoundToInt(unit.Position2.y + unit.UnitData.Size.y); j++)
                    {
                        if (i >= 0 && i < width && j >= 0 && j < height)
                            map[i, j].Add(unit);
                    }
                }
            }
        }
    }


    void updateFinish()
    {
        if (Finish) return;
        if (Hp <= 0 && !BattleManager.Instance.IsInfHealth)
        {
            Finish = true;
            Win = false;
        }
        else if (EnemyCount == 0)
        {
            Finish = true;
            Win = true;
        }
        if (Finish)
        {
            BattleManager.Instance.ReSetPreviwSetting();
            BattleManager.Instance.IsPreview = false;
            BattleUI.UI_Battle.Instance.BattleEnd();
        }
    }

    public void GiveUp()
    {   
        BattleManager.Instance.ReSetPreviwSetting();
        BattleManager.Instance.IsPreview = false;
        Finish = true;
        Win = false;
        BattleUI.UI_Battle.Instance.BattleEnd();
    }

    public void DoDamage(int count)
    {
        if (Hp <= 0) return;
        Hp -= count;
        Hurt += count;
    }

    public void Trigger(TriggerEnum triggerEnum)
    {
        RuleUnit.Trigger(triggerEnum);
        //foreach (var unit in PlayerUnits.ToArray())
        //{
        //    unit.Trigger(triggerEnum);
        //}
        //foreach (var enemy in Enemys)
        //{
        //    enemy.Trigger(triggerEnum);
        //}
        foreach (var unit in AllUnits.ToArray())
        {
            unit.Trigger(triggerEnum);
        }
    }

    public float NextFloat(float min,float max)
    {
        return (float)Random.NextDouble() * (max - min) + min;
    }
}

