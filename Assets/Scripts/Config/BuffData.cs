public class BuffData : IConfig 
{
      public string Id { get ; set ; }
      public string Type;
      public string Name;
      public int StopLess;
      public int StopNeed;
      public int RoundNeed;
      public bool UnSourceCheck;
      public bool IfSwitch;
      public int? LastingEffect;
      [ConfigRef("EffectData")] public string LastingEffectId;
      public float LastTime;
      public int? Upgrade;
      [ConfigRef("BuffData")] public string UpgradeId;
      public bool Resist;
      public bool DeadRemain;
      public int? RelyBuff;
      [ConfigRef("BuffData")] public string RelyBuffId;
      public bool NotCancelable;
      public System.Collections.Generic.Dictionary<string,object> Data;
}
