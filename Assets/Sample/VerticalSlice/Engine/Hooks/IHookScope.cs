#nullable enable
using System.Collections.Generic;
using VerticalSlice.Engine.Effects;
using VerticalSlice.Engine.Entities;
using VerticalSlice.Engine.Tags;

namespace VerticalSlice.Engine.Hooks
{
    /// <summary>
    /// What a hook handler receives alongside the in-flight command. Two
    /// dispatch surfaces, split by intent:
    ///
    /// <para><b>Inline</b> — <see cref="Run"/> / <see cref="FireDirect"/>.
    /// Runs the effect now, nested in the current scope. Use from
    /// <c>Before</c>-phase hooks that need their effect's body to complete
    /// before <c>Apply</c> — Divine Shield zeroing an Amount, Armor
    /// reducing it. The sub-effect's block appears as a wire child of the
    /// in-flight command.</para>
    ///
    /// <para><b>Stack</b> — <see cref="Schedule"/> / <see cref="ScheduleDirect"/>.
    /// Pushes the effect onto the engine stack as its own top-level
    /// resolution. Use from <c>After</c>-phase reactions (Deathrattle,
    /// Backlash) that should resolve as a separate frame. On the wire the
    /// reaction is a sibling of the triggering command, with the
    /// triggering command's id carried as <c>parentId</c> on the
    /// reaction's <c>BlockStart</c> so causality is preserved in metadata.
    /// </para>
    ///
    /// Convention in this slice: Before-phase = <see cref="Run"/>, After-phase
    /// = <see cref="Schedule"/>. The choice is a game-design lever — simpler
    /// games may resolve everything inline, MTG-like games may want
    /// everything stack-scheduled with priority passes.
    /// </summary>
    public interface IHookScope
    {
        // --- Inline (Before-phase / replacement style) -----------------------

        void Run(Effect effect);

        /// <summary>
        /// Inline variant: fire a direct trigger on a specific entity now.
        /// The looked-up effect runs nested in the current command's scope.
        /// </summary>
        void FireDirect<TTrigger>(IEntity entity, TTrigger trigger) where TTrigger : class;

        // --- Stack (After-phase / reaction style) ----------------------------

        void Schedule(Effect effect);

        /// <summary>
        /// Stack variant: push a direct-trigger lookup onto the engine
        /// stack. Used by engine bridges (e.g., MoveCard→Deathrattle) that
        /// want the per-card effect to resolve as its own top-level frame
        /// after the triggering command finishes.
        /// </summary>
        void ScheduleDirect<TTrigger>(IEntity entity, TTrigger trigger) where TTrigger : class;

        // --- Reads -----------------------------------------------------------

        IEntity? Get(int entityId);
        IEnumerable<IEntity> All();
        IEnumerable<IEntity> Where(TagKey<bool> marker);
    }
}
