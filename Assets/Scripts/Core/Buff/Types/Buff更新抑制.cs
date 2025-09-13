using UnityEngine;
using System.Linq;

namespace Buffs
{
    public class Buff更新抑制 : Buff
    {
        // 视觉特效引用
        //private Effect statusEffect;

        public override void Init()
        {
            base.Init();
            IsCancelable = false;
            /* 添加视觉特效
            statusEffect = EffectManager.Instance.GetEffect(
                Database.Instance.GetIndex<EffectData>("BUFF抵挡特效"));
            statusEffect.Init(Unit, Unit, Unit.Position, Unit.Direction);
            statusEffect.SetLifeTime(float.PositiveInfinity);
            */
        }

        public override void Apply()
        {
            base.Apply();
            IsCancelable = false;
            // 立即检查并抑制所有可被抵挡的BUFF
            SuppressCancelableBuffs();

           // Log.Debug($"{Unit.UnitData.Name} 获得了BUFF抵挡效果");
        }

        public override void Update()
        {
            base.Update();
            IsCancelable = false;
            /*更新视觉特效位置
            if (statusEffect != null)
            {
                statusEffect.SetPosition(Unit.Position);
            }

            // 持续检查并抑制新添加的可被抵挡BUFF
             这可以通过Unit.UpdateBuffSuppression方法处理，这里不需要额外逻辑*/
        }

        public override void Finish()
        {
            base.Finish();

            // 恢复所有被抑制的BUFF
            RestoreSuppressedBuffs();

            /*移除视觉特效
            if (statusEffect != null)
            {
                EffectManager.Instance.ReturnEffect(statusEffect);
                statusEffect = null;
            }

            Log.Debug($"{Unit.UnitData.Name} 失去了BUFF抵挡效果");*/
        }

        // 抑制所有可被抵挡的BUFF
        private void SuppressCancelableBuffs()
        {
            foreach (var buff in Unit.Buffs.Where(b => b.IsCancelable))
            {
                // 检查BUFF的施加者是否有"BUFF可抵挡"效果
                if (buff.OriginalCaster != null &&
                    buff.OriginalCaster.Buffs.Any(b => b is Buff更新抑制可应用 && !b.IsSuppressed))
                {
                    buff.IsSuppressed = true;
                    Unit.OnBuffSuppressed(buff);
                }
            }
        }

        // 恢复所有被抑制的BUFF
        private void RestoreSuppressedBuffs()
        {
            foreach (var buff in Unit.Buffs.Where(b => b.IsSuppressed))
            {
                // 检查BUFF是否是因为这个抵挡效果而被抑制的
                if (buff.IsCancelable &&
                    buff.OriginalCaster != null &&
                    buff.OriginalCaster.Buffs.Any(b => b is Buff更新抑制可应用 && !b.IsSuppressed))
                {
                    buff.IsSuppressed = false;
                    Unit.OnBuffRestored(buff);
                }
            }
        }
    }
}