using System;

namespace Framework.Wire
{
    // The UnityLinker matches this attribute by class name, not by namespace. Do not rename it.
    // Framework.Wire has no engine reference, so it cannot use UnityEngine.Scripting.PreserveAttribute.
    [AttributeUsage(AttributeTargets.Method)]
    internal sealed class PreserveAttribute : Attribute { }
}
