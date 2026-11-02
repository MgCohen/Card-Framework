using System;

namespace Framework.Stripping
{
    // The UnityLinker matches this attribute by class name, not by namespace. Do not rename it.
    // It keeps each derived class or record in a stripped player. It has no effect on an interface.
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class RequireDerivedAttribute : Attribute { }
}
