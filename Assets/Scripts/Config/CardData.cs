public class CardData : IConfig 
{
      public string Id { get ; set ; }
      public int[] units;
      [ConfigRef("UnitData")] public string[] unitsIds;
}
