using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using System.Linq.Dynamic.Core;
using System.Linq.Dynamic.Core.Parser;
using System.Linq;
using System.Linq.Dynamic.Core.CustomTypeProviders;

// 自定义类型提供器，公开AdditionalTypes
public class CustomDynamicLinqTypeProvider : DefaultDynamicLinqCustomTypeProvider
{
    // 公开基类的受保护成员AdditionalTypes
    public new IList<Type> AdditionalTypes => base.AdditionalTypes;
}
