using System;
using Units;

namespace Skills
{
    public class 重置动画 : Skill
    {
        public override void Effect(Unit target)
        {
            base.Effect(target);
            if (target is 干员 && target.UnitModel is PlayerUnitModel && target.UnitData.DefaultAnimation != null)
            {
                PlayerUnitModel playerUnitModel = target.UnitModel as PlayerUnitModel;
                playerUnitModel.SkeletonAnimation2.state.ClearTracks();
                playerUnitModel.SkeletonAnimation2.state.SetAnimation(0, target.UnitData.DefaultAnimation[0], false);
                playerUnitModel.SkeletonAnimation.Initialize(true, false);
            }
        }
    }
}