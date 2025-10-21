using FairyGUI;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

public enum IconType
{
    HeadIcon,
    SkillIcon,
    ContractIcon,
    RelicIcon
}
public static class IconHelper
{
    public static string ToHeadIcon(this string icon)
    {
        return "ui://Res/" + icon;
    }
    public static string ToSkillIcon(this string icon)
    {
        return "ui://SkillIcon/" + icon;
    }
    public static string ToContractIcon(this string icon)
    {
        return "ui://Res/" + icon;
    }

    public static string ToRelicIcon(this string icon)
    {
        return "ui://Res/" + icon;
    }

    public static void SetTexture(GLoader loader, string icon_url, IconType? type = null)
    {
        if (string.IsNullOrEmpty(icon_url))  // 增加参数校验
        {
            loader.url = "";
            //Debug.LogError("Icon url is null or empty!");
            //TipManager.Instance.ShowTip("Icon url is null or empty!");
            return;
        }

        bool isExTexture = icon_url.StartsWith("Extexture:");

        if (isExTexture)
        {
            try
            {
                ExtextureLoader.Instance.LoadLocalTexture(loader, icon_url.Substring(11));
                //Debug.Log("Load texture success!, path: " + icon_url.Substring(11));
            }
            catch (Exception e)
            {
                Debug.LogError(e);
                TipManager.Instance.ShowTip("Load texture failed!, path: " + icon_url.Substring(11));
                TipManager.Instance.ShowTip(e.Message);
            }
            return;
        }
        string url = type switch
        {
            IconType.HeadIcon => icon_url.ToHeadIcon(),
            IconType.SkillIcon => icon_url.ToSkillIcon(),
            IconType.ContractIcon => icon_url.ToContractIcon(),
            IconType.RelicIcon => icon_url.ToRelicIcon(),
            _ => "ui://Res/" + icon_url  // 包括 type 为 null 或未定义的情况
        };
        loader.url = url;
        //Debug.Log("Set texture success!, url: " + url);
    }
}
