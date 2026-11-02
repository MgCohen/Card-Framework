using System;

namespace Framework.Stripping
{
    // The UnityLinker matches this attribute by class name, not by namespace. Do not rename it.
    // It keeps the marked member in a stripped player. Engine-free assemblies use it in place of
    // UnityEngine.Scripting.PreserveAttribute.
    public sealed class PreserveAttribute : Attribute { }
}
