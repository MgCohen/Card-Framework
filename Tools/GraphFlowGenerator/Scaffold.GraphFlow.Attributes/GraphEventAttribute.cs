using System;

namespace Scaffold.GraphFlow
{
    /// <summary>
    /// Marks a class as a GraphFlow event — appears in the OnTrigger node's event-type dropdown
    /// at edit time. The class's public instance fields become output ports on OnTrigger
    /// (in the graph editor) when the user picks this type.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public sealed class GraphEventAttribute : Attribute { }
}
