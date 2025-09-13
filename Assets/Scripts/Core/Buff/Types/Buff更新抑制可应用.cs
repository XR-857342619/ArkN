using UnityEngine;

namespace Buffs
{
    public class Buff更新抑制可应用 : Buff
    {
        public override void Init()
        {
            base.Init();
            MakesBuffsCancelable = true;
            IsCancelable = false;
            
            // 这个BUFF本身不需要特殊逻辑，它的作用在AddBuff时判断
            // 但可以添加一些视觉效果来表示单位有这个状态

            /*添加视觉特效
            statusEffect = EffectManager.Instance.GetEffect(
                Database.Instance.GetIndex<EffectData>("BUFF可抵挡特效"));
            statusEffect.Init(Unit, Unit, Unit.Position, Unit.Direction);
            statusEffect.SetLifeTime(float.PositiveInfinity);
            */
        }

        public override void Apply()
        {
            base.Apply();
            // 这个BUFF本身可能有一些附加效果
            // 例如：改变单位的外观，显示特殊图标等
            //Log.Debug($"{Unit.UnitData.Name} 获得了BUFF可抵挡效果");
        }

        public override void Update()
        {
            base.Update();
            /*更新视觉特效位置
            if (statusEffect != null)
            {
                statusEffect.SetPosition(Unit.Position);
            }*/
        }

        public override void Finish()
        {
            base.Finish();

            /*移除视觉特效
            if (statusEffect != null)
            {
                EffectManager.Instance.ReturnEffect(statusEffect);
                statusEffect = null;
            }

            Log.Debug($"{Unit.UnitData.Name} 失去了BUFF可抵挡效果");*/
        }
    }
}