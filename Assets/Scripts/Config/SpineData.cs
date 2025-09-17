public class SpineData : IConfig 
{
      public string Id { get ; set ; }
      public bool OnlyFront;
      public bool UseAppHotfixResPath;
      public string FrontAtlasPath;
      public string FrontPngPath;
      public string FrontSkelPath;
      public string? BackAtlasPath;
      public string? BackPngPath;
      public string? BackSkelPath;
}
