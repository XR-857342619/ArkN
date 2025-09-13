using System;

namespace Buffs
{

    public class 精确移除Buff : Buff
    {

        public override void Init()
        {
            base.Init();
            object[] array = base.BuffData.Data.GetArray("Buffs");
            this.targetBuffIds = new int[array.Length];
            for (int i = 0; i < array.Length; i++)
            {
                string id = Convert.ToString(array[i]);
                this.targetBuffIds[i] = Database.Instance.GetIndex<BuffData>(id);
            }
        }


        public override void Update()
        {
            base.Update();
            for (int i = this.Unit.Buffs.Count - 1; i >= 0; i--)
            {
                Buff buff = this.Unit.Buffs[i];
                if (Array.IndexOf<int>(this.targetBuffIds, buff.Id) != -1)
                {
                    buff.Finish();
                }
            }
        }
        private int[] targetBuffIds;
    }
}