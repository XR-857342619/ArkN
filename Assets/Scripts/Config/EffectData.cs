public class EffectData : IConfig 
{
      public string Id { get ; set ; }
      public string Prefab;
      public int StartPos;
      public UnityEngine.Vector3 Offset;
      public string BindPoint;
      public bool BoneFollow;
      public bool ForwardOnly;
      public int ParentFollow;
      public int ScaleXFollow;
      public int ForwordDirection;
      public float FaceCamera;
      /// <summary>存活秒数；表里不填时默认 5 秒（与 Effect.LifeTime 的初值一致），
      /// 否则 0 会让 Effect.Update 第一帧就判定到期并立刻回池。</summary>
      public float LifeTime = 5f;
}
