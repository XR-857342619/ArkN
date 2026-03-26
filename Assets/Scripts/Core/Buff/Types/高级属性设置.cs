using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Buffs
{
    public class 高级属性设置:Buff
    {
        protected string[] names;
        protected float[] values;
        protected string func;
        List<object> orgvalues = new List<object>();
        public override void Init()
        {
            base.Init();
            var datas = BuffData.Data.GetArray("t");
            var value = BuffData.Data.GetArray("v");
            names = new string[datas.Length];
            values = new float[datas.Length];
            for (int i = 0; i < datas.Length; i++)
            {
                names[i] = Convert.ToString(datas[i]);
                values[i] = Convert.ToSingle(value[i]);
            }
        }

        public override void ApplyToUnit()
        {
            for (int i = 0; i < names.Length; i++)
            {
                string fieldName = (string)names[i];
                var field = Unit.GetType().GetField(fieldName);
                if (field == null)
                {
                    Log.Debug($"{Unit.UnitData.Id} 没有 属性 {fieldName}");
                    continue;
                }
                float baseValue = (float)field.GetValue(Unit);
                orgvalues.Add(baseValue);
                if (values.Length > 0)
                    field.SetValue(Unit, values[i]);
                else
                    field.SetValue(Unit, GetValue(i));
                //UnityEngine.Debug.Log($"{Unit.UnitData.Id}的{names[i]}变成{field.GetValue(Unit)}");
            }
        }
        public override void Finish()
        {
            base.Finish();
            for (int i = 0; i < names.Length; i++)
            {
                string fieldName = (string)names[i];
                var field = Unit.GetType().GetField(fieldName);
                field.SetValue(Unit, orgvalues[i]);
            }
        }
        protected virtual float GetValue(int i)
        {
            return Skill.SkillData.GetBuffData(Index)[i];
        }
    }
}
