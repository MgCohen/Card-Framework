using System;

namespace Scaffold.GraphFlow
{
    /// <summary>
    /// Opt-in marker — tagged public fields become typed ports on the generated runtime/editor mirror.
    /// Untagged fields are runtime-only data (not visible in the editor / no port wires).
    /// <para>Post-M3 phase 2 (decision #4): port IDs are field names. The optional <see cref="Name"/>
    /// override is reserved for migration scenarios (rename a field without breaking already-baked
    /// graphs) — unused in v1.</para>
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class GraphPortAttribute : Attribute
    {
        /// <summary>Optional override — when set, the port name in the registry uses this value instead of the field name. Reserved for migrations.</summary>
        public string? Name { get; set; }
    }
}
