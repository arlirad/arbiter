using System;
using System.Reflection;
using System.Runtime.Loader;

namespace Arbiter
{
    public class AHtmlLoadContext : AssemblyLoadContext
    {
        public Assembly? Load(string context)
        {
            return null;
        }
    }
}