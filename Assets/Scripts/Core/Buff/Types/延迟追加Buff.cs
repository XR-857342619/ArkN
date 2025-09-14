using System;

namespace Buffs
{
    public class 延迟追加Buff : Buff
    {
        public override void Init()
        {
            base.Init();
            this.delayTime = base.BuffData.Data.GetFloat("Delay", 0f);
            if (this.delayTime <= 0f)
            {
                this.Finish();
            }
            this.delayTimer.Set(this.delayTime);
            object[] array = base.BuffData.Data.GetArray("Buffs");
            this.buffNames = new string[array.Length];
            for (int i = 0; i < this.buffNames.Length; i++)
            {
                this.buffNames[i] = Convert.ToString(array[i]);
            }
        }

        public override void Update()
        {
            base.Update();
            if (!this.delayTimer.Update(SystemConfig.DeltaTime))
            {
                return;
            }
            for (int i = 0; i < this.buffNames.Length; i++)
            {
                int buffId = Database.Instance.GetIndex<BuffData>(this.buffNames[i]);
                this.Unit.AddBuff(buffId, this.Skill,0);
            }
        }

        protected string[] buffNames;
        private CountDown delayTimer = new CountDown(0f);
        private float delayTime;
    }
}