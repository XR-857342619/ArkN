using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Skills
{
    public class 暂停 : Skill
    {
        public override void Cast()
        {
            base.Cast();
            TimeHelper.Instance.SetPause(true);
        }
        protected override void OnOpenEnd()
        {
            base.OnOpenEnd();
            TimeHelper.Instance.SetPause(false);
        }
    }
}
