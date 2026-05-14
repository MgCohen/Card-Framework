#nullable enable
using System.Collections.Generic;
using VerticalSlice.Engine.Effects;
using VerticalSlice.Engine.Entities;
using VerticalSlice.Engine.Tags;

namespace VerticalSlice.Engine.Hooks
{
    /// <summary>
    /// What a hook handler receives alongside the in-flight command. Read
    /// access to entities and the ability to <see cref="Run"/> reactive
    /// effects. Hooks mutate the in-flight command directly via the
    /// command reference passed alongside the scope (Replacement-band
    /// hooks zero an Amount; Reaction-band hooks fire reactive effects).
    ///
    /// <b>No Execute</b> — hooks don't dispatch new commands directly.
    /// To do new state writes, hooks Run an Effect, which composes commands.
    /// This keeps the Block hierarchy clean: every reactive write traces
    /// back to an Effect block, never to a "stray" Execute mid-hook.
    /// </summary>
    public interface IHookScope
    {
        void Run(Effect effect);

        /// <summary>
        /// Fire a direct trigger on a specific entity. Used by engine
        /// bridges subscribed at the bus that translate broadcast events
        /// into targeted per-card direct triggers (e.g., MoveCard→Deathrattle).
        /// </summary>
        void FireDirect<TTrigger>(IEntity entity, TTrigger trigger) where TTrigger : class;

        IEntity? Get(int entityId);
        IEnumerable<IEntity> All();
        IEnumerable<IEntity> Where(TagKey<bool> marker);
    }
}
