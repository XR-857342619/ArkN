using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Buffs
{
    public class 覆盖动作 : Buff
    {
        public override void Init()
        {
            base.Init();

            bool allOverWrite = BuffData.Data.GetBool("AllOverWrite");
            string[] keys = { "StartAnimation", "IdleAnimation", "DieAnimation", "MoveAnimation" };

            foreach (string key in keys)
            {
                var data = BuffData.Data.GetArray(key);
                if (data == null) continue;

                // 将数据转换为字符串数组
                string[] names = new string[data.Length];
                for (int i = 0; i < data.Length; i++)
                {
                    names[i] = Convert.ToString(data[i]);
                }

                // 如果开启全部覆盖，则所有动画都赋给 OverWriteAnimation（保留原逻辑：后面的覆盖前面的）
                if (allOverWrite)
                {
                    Unit.OverWriteAnimation = names;
                }
                else
                {
                    // 分别赋值给对应的覆盖字段
                    if (key == "StartAnimation") Unit.OverWriteStart = names;
                    else if (key == "IdleAnimation") Unit.OverWriteIdle = names;
                    else if (key == "DieAnimation") Unit.OverWriteDie = names;
                    else if (key == "MoveAnimation") Unit.OverWriteMove = names;
                }
            }

            // 强制刷新当前动画状态（如果当前处于 Start 状态，就会应用新的覆盖）
            Unit.SetStatus(Unit.State);
        }

        public override void Finish()
        {
            base.Finish();
            // 清除所有覆盖字段（注意新增了 Start）
            Unit.OverWriteAnimation = null;
            Unit.OverWriteStart = null;
            Unit.OverWriteIdle = null;
            Unit.OverWriteDie = null;
            Unit.OverWriteMove = null;
        }
    }
}