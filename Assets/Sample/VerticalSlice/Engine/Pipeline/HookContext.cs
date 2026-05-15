#nullable enable
using VerticalSlice.Engine.Commands;
using VerticalSlice.Engine.Effects;

namespace VerticalSlice.Engine.Pipeline
{
    /// <summary>
    /// What a <see cref="HookConfig.IsActive"/> check sees when the engine
    /// asks "should this hook fire for the in-flight command?". Tiny by
    /// design — growable later without breaking signatures. Passed by
    /// <c>in</c>-ref so the check is zero-alloc on the hot path.
    ///
    /// <para><see cref="Command"/> is the in-flight command, untyped — the
    /// concrete game-side <see cref="HookConfig"/> subclass pattern-matches
    /// if it needs to read command fields (e.g. "only when MY OWNER takes
    /// damage" reads <c>cmd is DealDamage dd &amp;&amp; dd.Target.OwnerId() ==
    /// Self.OwnerId()</c>).</para>
    ///
    /// <para><see cref="Scope"/> is the same <see cref="IEffectScope"/>
    /// effects see — gives <c>IsActive</c> read access to entities, tags,
    /// and other world state without needing its own surface. The check
    /// should treat the scope as read-only (don't dispatch from inside an
    /// IsActive check).</para>
    /// </summary>
    public readonly struct HookContext
    {
        public Command Command { get; init; }
        public IEffectScope Scope { get; init; }
    }
}
