using System;

namespace Framework.Routing
{
    // The UnityLinker matches this attribute by class name, not by namespace. Do not rename it.
    // It keeps each implementor of the interface in a stripped player.
    [AttributeUsage(AttributeTargets.Interface)]
    public sealed class RequireImplementorsAttribute : Attribute { }
}
