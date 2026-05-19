using System;

namespace Scaffold.GraphFlow
{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class InAttribute : Attribute
    {
    }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class OutAttribute : Attribute
    {
    }
}
