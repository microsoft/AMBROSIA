using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ambrosia
{
    [AttributeUsage(AttributeTargets.Interface, Inherited = false, AllowMultiple = false)]
    public class InstanceProxyAttribute : Attribute
    {
        public InstanceProxyAttribute(Type proxyFor) { }
    }
}
