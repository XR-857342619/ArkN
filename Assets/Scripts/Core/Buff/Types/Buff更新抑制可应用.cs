using UnityEngine;

namespace Buffs
{
    public class Buff更新抑制可应用 : Buff
    {
        public override void Init()
        {
            base.Init();

            MakesBuffsCancelable = true;

            // 可选：视觉特效
            /*
            var effectId = Database.Instance.GetIndex<EffectData>("BUFF抵挡特效");
            var statusEffect = EffectManager.Instance.GetEffect(effectId);
            statusEffect.Init(Unit, Unit, Unit.Position, Unit.Direction);
            statusEffect.SetLifeTime(float.PositiveInfinity);
            */
        }
    }
}
