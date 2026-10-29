public class ModifyData : IConfig 
{
      public string Id { get ; set ; }
      public string Type;
      public int? Buff;
      [ConfigRef("BuffData")] public string BuffId;
      public System.Collections.Generic.Dictionary<string,object> Data;
}
