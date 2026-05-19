using System;

namespace Scaffold.GraphFlow
{
    /// <summary>
    /// Opt-out marker — under <c>PortConvention.AllFieldsIn</c> a tagged field is excluded from the
    /// generated port set (runtime-only data, not visible in the editor / no port wires). Has no
    /// effect under <c>PortConvention.AttributedFields</c> (which already requires explicit
    /// <c>[GraphPort]</c> opt-in).
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class GraphPortIgnoreAttribute : Attribute
    {
    }
}
