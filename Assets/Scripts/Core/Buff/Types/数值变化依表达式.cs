using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Buffs
{
    public class 数值变化依表达式 : Buff
    {
        public string expression;
        public ExpressionExecutor evaluator;
        public override void Init()
        {
            base.Init();
            expression = BuffData.Data.GetStr("Expression", string.Empty);
            evaluator = new ExpressionExecutor(this);
        }
        
        public override void Apply()
        {
            //base.Update();
            if (string.IsNullOrEmpty(expression))
            {
                Finish();
                return;
            }
            evaluator.ExecuteAssignment(expression);
            Log.Debug(Skill.SkillData.DamageRate.ToString());
        }
    }
}
