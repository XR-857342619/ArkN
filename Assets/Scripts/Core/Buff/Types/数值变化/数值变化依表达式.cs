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
        public int time;
        //public ExpressionExecutor evaluator;
        public UnifiedExpressionEngine evaluator;
        public override void Init()
        {
            base.Init();
            expression = BuffData.Data.GetStr("Expression", string.Empty);
            time = BuffData.Data.GetInt("Time", -1);
            //evaluator = new ExpressionExecutor(this);
            evaluator = new UnifiedExpressionEngine(this);
        }
        
        public override void ApplyToUnit()
        {
            //base.Update();
            if (string.IsNullOrEmpty(expression) || time == 0)
            {
                //Finish();
                return;
            }
            evaluator.ExecuteAssignment(expression);
            time--;
            //Log.Debug(time);
        }

        public override void ApplyToBullet()
        {
            //base.Update();
            if (string.IsNullOrEmpty(expression) || time == 0)
            {
                //Finish();
                return;
            }
            evaluator.ExecuteAssignment(expression);
            time--;
            //Log.Debug(time);
        }

        public override void Reset()
        {
            base.Reset();
            time = BuffData.Data.GetInt("Time", -1);
        }
        //public override void Update()
        //{
        //    base.Update();
        //    if (string.IsNullOrEmpty(expression) || time == 0)
        //    {
        //        Finish();
        //        //return;
        //    }
        //}
    }
}
