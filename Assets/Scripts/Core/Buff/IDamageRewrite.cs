using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


public interface IDamageRewrite
{
    void DamageRewrite(DamageInfo damageInfo);

    public int OrderCode { get; }
}
