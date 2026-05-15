#nullable enable
namespace VerticalSlice.Engine.Pipeline
{
    /// <summary>
    /// Registration metadata for a hook subscription. Bundles the three
    /// things the engine needs to know about *when* a hook fires —
    /// <see cref="Phase"/> (Before/After Apply), <see cref="Priority"/>
    /// (ordering within phase), and <see cref="IsActive"/> (cheap
    /// pre-filter, runs before any factory allocation).
    ///
    /// <para><b>Base class works for game-wide rules.</b> Default
    /// <see cref="IsActive"/> returns true unconditionally — so passing a
    /// plain <c>new HookConfig { Phase = …, Priority = … }</c> gives you a
    /// hook that's always live (DivineShield, Armor, Backlash in this
    /// slice).</para>
    ///
    /// <para><b>Subclass for gating patterns.</b> Card-attached hooks
    /// that should only fire while the card is in a particular zone, or
    /// on a particular player's turn, or when the in-flight command
    /// targets the card's owner, are expressed as game-defined
    /// subclasses that override <see cref="IsActive"/>. The framework
    /// never reaches into game-specific concepts (zones, turn state,
    /// owners) — it just asks the config "are you live right now?" and
    /// trusts the answer.</para>
    ///
    /// <para><b>IsActive vs Effect.Validate.</b> Two filters with
    /// different jobs. <see cref="IsActive"/> is "should this hook be
    /// considered at all for this fire?" — typically zone / turn /
    /// ownership gating that doesn't depend on the per-call payload.
    /// <c>Effect.Validate</c> is "given we're firing, do per-call
    /// conditions hold?" — typically payload-dependent (Amount &gt; 0,
    /// target has shield, etc.). <see cref="IsActive"/> runs before the
    /// factory allocates the Effect; <c>Validate</c> runs after, just
    /// before the wire Block opens.</para>
    /// </summary>
    public class HookConfig
    {
        public HookPhase Phase { get; init; }
        public HookPriority Priority { get; init; } = HookPriority.OnEffect;

        /// <summary>
        /// Optional diagnostic label — appears alongside the subscription
        /// in tooling that surfaces "which configs gated which fires."
        /// Cheap to populate; recommended even for game-wide rules.
        /// </summary>
        public string? Source { get; init; }

        /// <summary>
        /// "Should this hook fire for the in-flight command?" Cheap
        /// pre-filter — runs before any Effect factory allocation. Base
        /// returns true; game subclasses override to gate.
        /// </summary>
        public virtual bool IsActive(in HookContext ctx) => true;
    }
}
