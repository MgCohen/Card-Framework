using System;

namespace Scaffold.GraphFlow
{
    /// <summary>
    /// Marks a hand-written <c>RuntimeNode</c> (data node) or <c>RuntimeNode&lt;TRunner&gt;</c> (flow node)
    /// for generic-node emission. The author writes the typed port-handle surface (<c>InputPort&lt;T&gt;</c>,
    /// <c>OutputPort&lt;T&gt;</c>, <c>FlowOut</c>) plus the <c>Execute</c> body for flow nodes; the generator
    /// emits the default ctor that constructs the port handles, calls a user-defined
    /// <c>partial void InitializePorts()</c> body when the class has any <c>OutputPort</c> fields, and
    /// populates the <c>Ports</c> dictionary on <c>RuntimeNode</c>. The generator also emits the editor
    /// mirror (<c>&lt;Name&gt;EditorNode</c>) and the per-package registry entry.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class GraphNodeAttribute : Attribute
    {
        /// <summary>Optional category for editor menu grouping. Visual cleanup (display name, icon, full menu API) is M4 polish.</summary>
        public string? Category { get; set; }
    }
}
