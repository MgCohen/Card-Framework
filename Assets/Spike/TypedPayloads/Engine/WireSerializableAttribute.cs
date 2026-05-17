#nullable enable
using System;

namespace TypedPayloads.Engine
{
    /// <summary>
    /// Marks a struct as a tag value type that should get a generated codec.
    /// Today this is a documentation contract — codecs are hand-written until
    /// the source generator project lands. The attribute is the discovery
    /// hook the generator will scan for.
    /// </summary>
    [AttributeUsage(AttributeTargets.Struct)]
    public sealed class WireSerializableAttribute : Attribute { }
}
