#nullable enable
using TypedPayloads.Engine;

namespace TypedPayloads.Sample
{
    /// <summary>
    /// Tag declarations for the spike. Demonstrates the three codec routes:
    /// <list type="bullet">
    /// <item><see cref="Health"/> — <c>int</c> → <c>I64</c> variant.</item>
    /// <item><see cref="IsMinion"/> — <c>bool</c> → <c>I64</c> variant (0/1).</item>
    /// <item><see cref="Pos"/> — <see cref="Vector3"/> → <c>Bytes</c> variant via custom codec.</item>
    /// </list>
    /// Adding a new tag is one line: <c>TagRegistry.Allocate&lt;T&gt;("Name")</c> plus a
    /// codec registration in <see cref="Codecs"/>.
    /// </summary>
    public static class Tags
    {
        public static readonly TagKey<int>     Health   = TagRegistry.Allocate<int>("Health");
        public static readonly TagKey<bool>    IsMinion = TagRegistry.Allocate<bool>("IsMinion");
        public static readonly TagKey<Vector3> Pos      = TagRegistry.Allocate<Vector3>("Pos");
    }
}
