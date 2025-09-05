using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Units;
using UnityEngine;

namespace Skills
{
    public class 显示进度 : Skill
    {
        public 干员 Operator;
        public 干员 skilloprator;
        public Vector2Int pos;
        public DirectionEnum direction;
        public override void Start()
        {
            base.Start();

        }
    }
}
