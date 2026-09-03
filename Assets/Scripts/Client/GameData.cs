using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class GameData
{
    public static GameData Instance => instance != null ? instance : instance = new GameData();
    private static GameData instance;

    public List<Card> Cards = new List<Card>();
    public Team[] Teams = new Team[4];
    public string Name;
    //public string MainPageUnitId;
    public float Bgm = 1;
    public bool showHP = false;
    public bool showElement = false;
    public List<string> ExcelList = new List<string>();
    public List<string> ExcludedExcelList = new List<string>();
    /// <summary>
    /// 仅从存档中读取 ExcelList（排除列表），不触发完整的 GameData 初始化。
    /// 用于启动时在 Database 加载前取得需要排除的数据文件列表。
    /// </summary>
    /// <summary>
    /// 根据当前 ExcelList（已选择列表）计算排除列表：
    /// 全部 Excel 文件路径 - ExcelList。
    /// </summary>
    public void RefreshExcludedExcelList()
    {
        var all = new List<string>();
        foreach (var folder in Database.Instance.GetExcelPathList())
        {
            all.AddRange(Database.Instance.GetExcelFileList(folder));
        }
        if ((ExcelList?.Count() ?? 0) > 0)
            ExcludedExcelList = all.Except(ExcelList).ToList();
        else
            ExcludedExcelList = all;
    }

    public void LoadExcelListFromSave()
    {
        var str = SaveHelper.LoadFile("/data.sav");
        if (string.IsNullOrEmpty(str)) return;

        try
        {
            var saved = JsonHelper.FromJson<GameData>(str);
            if (saved?.ExcelList == null) return;

            ExcelList.Clear();
            foreach (var item in saved.ExcelList)
            {
                string normalized = PathHelper.NormalizeAppPath(item);
                if (!string.IsNullOrEmpty(normalized) && !ExcelList.Contains(normalized))
                    ExcelList.Add(normalized);
            }
            //Debug.Log(string.Join(";", ExcelList));
            RefreshExcludedExcelList();
        }
        catch (Exception e)
        {
            Debug.LogError($"读取存档 ExcelList 失败: {e.Message}");
        }
    }

    public void Init()
    {
        //Debug.Log("GameData初始化");
        var str = SaveHelper.LoadFile("/data.sav");
        if (!string.IsNullOrEmpty(str))
        {
            //Debug.Log(str);
            //try
            //{
                instance = JsonHelper.FromJson<GameData>(str);
                LoadTeamDataFromSave();
            //}
            //catch (Exception e)
            //{
            //    Debug.LogError($"读取存档失败，错误信息:\n{e}");
            //    TipManager.Instance.initErorrTips.Add("读取存档失败:"+e.Message);
            //}
        }

        List<int> ids = new List<int>();
        for (int i = instance.Cards.Count - 1; i >= 0; i--)
        {
            try
            {
                if (ids.Contains(instance.Cards[i].Id) || instance.Cards[i].Id == -1)
                {
                    instance.Cards.RemoveAt(i);
                }
                else
                    ids.Add(instance.Cards[i].Id);
            }
            catch (Exception e)
            {
                Debug.LogError(e);
                TipManager.Instance.initErorrTips.Add("读取存档编队数据失败:" + e.Message);
                //TipManager.Instance.ShowTip("读取Card数据失败:" + e.Message);
            }
        }
        //CardData[] tmp = Database.Instance.GetAll<CardData>();
        //Debug.Log(tmp[90]);
        //Debug.Log(tmp[91]);
        //Debug.Log(tmp.Length);
        //int times = 0;
        
        LoadCardDataFromExcel();

        if (instance.Teams[0] == null)
        {
            for (int i = 0; i < Instance.Teams.Length; i++)
            {
                instance.Teams[i] = new Team();
            }
            //MainPageUnitId = Cards[0].UnitId;
            Name = "玩家名字";
            SaveHelper.SaveData();
        }
        if (instance.ExcelList == null)
        {
            //Debug.Log("初始化ExcelList");
            instance.ExcelList = new List<string>();
        }
        else
        {
            List<string> toRemove = new List<string>();
            List<string> fixedList = new List<string>();
            foreach (var item in instance.ExcelList)
            {
                // 规范化路径，修复 Android 上缺少 /storage/emulated/0 前缀的情况
                string normalized = PathHelper.NormalizeAppPath(item);
                if (!System.IO.File.Exists(normalized))
                {
                    //Debug.Log("删除无效的Excel文件路径：" + item);
                    toRemove.Add(item);
                }
                else
                {
                    fixedList.Add(normalized);
                }
            }
            instance.ExcelList.RemoveAll(x => toRemove.Contains(x));
            for (int i = 0; i < fixedList.Count; i++)
                instance.ExcelList[i] = fixedList[i];
            ExcelList = instance.ExcelList;
            RefreshExcludedExcelList();
            //Debug.Log("读取ExcelList成功");
            //foreach (var item in ExcelList)
            //{
            //    Debug.Log(item);
            //}
        }
    }
    private void LoadTeamDataFromSave()
    {
        if (instance.Teams is null) return;
        foreach (var t in instance.Teams)
        {
            if (t is null || t.Cards is null || t.Cards.Count == 0) continue;

            var a = t.Cards.ToArray();
            t.Cards.Clear();
            foreach (var c in a)
            {
                if (c == null)
                {
                    Debug.LogWarning("编队数据中存在 null 元素，已跳过");
                    TipManager.Instance.ShowTip("编队数据中存在 null 元素，已跳过");
                    continue;
                }
                //t.Cards.Add(instance.Cards.LastOrDefault(x => x.UnitId == c.UnitId));
                var matchedCard = instance.Cards.LastOrDefault(x => x?.UnitId == c.UnitId);
                if (matchedCard == null)
                {
                    Debug.LogWarning($"未找到 UnitId 为 {c.UnitId} 的单位，已跳过");
                    TipManager.Instance.ShowTip("干员列表变动, " + c.UnitId + "已移除");
                    continue;
                }

                t.Cards.Add(matchedCard);
            }
        }
    }
    private void LoadCardDataFromExcel()
    {
        //Debug.Log("开始读取卡牌数据");
        foreach (var unitConfig in Database.Instance.GetAll<CardData>())
        {
            //Debug.Log(unitConfig.Id);
            //Debug.Log(times);
            //times++;
            //try
            //{
                if (unitConfig == null) continue;
                if (instance.Cards.Any(x => unitConfig.units.Contains(x.Id))) continue;
                var unitdata = Database.Instance.Get<UnitData>(unitConfig.units.Last());
                if (unitdata is null)
                {
                    TipManager.Instance.initErorrTips.Add($"加载{unitConfig.Id}Card数据失败");
                    continue;
                }
                Card card = new Card()
                {
                    UnitId = unitdata.Id,
                    Level = unitdata.Level,
                    Upgrade = unitdata.Upgrade,
                };
                if (card.UnitData.MainSkill != null) card.DefaultUsingSkill = card.UnitData.MainSkill.Length - 1;
                instance.Cards.Add(card);
            //}
            //catch (Exception e)
            //{
            //    Debug.LogError(e);
            //    TipManager.Instance.initErorrTips.Add("读取Excel编队数据失败:" + e.Message);
            //}
        }
    }
    public void RefreshCardData()
    {
        instance.Cards.Clear();
        LoadCardDataFromExcel();
        LoadTeamDataFromSave();
        SaveHelper.SaveData();
    }
}
