using System;

namespace Scaffold.GraphFlow
{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class GraphHiddenAttribute : Attribute
    {
    }
}
