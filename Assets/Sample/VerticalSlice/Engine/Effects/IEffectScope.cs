#nullable enable
using System.Collections.Generic;
using VerticalSlice.Engine.Commands;
using VerticalSlice.Engine.Entities;
using VerticalSlice.Engine.Tags;

namespace VerticalSlice.Engine.Effects
{
    /// <summary>
    /// What an <see cref="Effect"/> sees inside its <see cref="Effect.Run"/>
    /// body. Read access to entities, the ability to dispatch primitive
    /// commands via <see cref="Execute"/>, and the ability to fire a
    /// direct trigger on a specific entity via <see cref="FireDirect"/>.
    ///
    /// <b>No entity.Set, no Subscribe</b> — the surface is "issue commands,
    /// fire direct triggers, read state for branching." Effects compose
    /// other effects through Execute (commands) or FireDirect (the
    /// engine's direct-trigger pathway); they don't reach the bus directly.
    /// </summary>
    public interface IEffectScope
    {
        void Execute(Command command);

        /// <summary>
        /// Run a sub-effect inline. Used by rule-effects (DivineShield
        /// fires <c>ShieldBreak</c>, etc.) and effect composition in
        /// general. The sub-effect goes through the same Validate/Run/
        /// block pipeline as a top-level dispatch.
        /// </summary>
        void Run(Effect effect);

        /// <summary>
        /// Fire a direct trigger on a specific entity. Engine looks up the
        /// entity's effect for <typeparamref name="TTrigger"/> (registered
        /// via the card-effect store) and runs it with the supplied
        /// payload. No-op if the entity has no effect for that trigger.
        ///
        /// Used by engine bridges that translate broadcast events into
        /// targeted dispatches — e.g. "MoveCard.dest=graveyard" calls
        /// <c>scope.FireDirect(card, new Deathrattle(card))</c>.
        /// </summary>
        void FireDirect<TTrigger>(IEntity entity, TTrigger trigger) where TTrigger : class;

        /// <summary>
        /// Test whether <paramref name="entity"/> has a registered factory
        /// for <typeparamref name="TTrigger"/> without firing it. Used by
        /// hook effects that translate broadcast events into direct
        /// triggers (e.g. <c>DeathrattleBridge</c>) — letting them gate
        /// in <c>Validate</c> so no wire block opens for a no-op fire.
        /// </summary>
        bool HasDirect<TTrigger>(IEntity entity) where TTrigger : class;

        /// <summary>
        /// Open a player-decision request. Emits a Request-kind block on
        /// the wire (one OptionEvent per option declared); the request
        /// sits in the engine's RequestsService.Open dictionary until a
        /// Response dispatches. Effects that open a request typically
        /// return immediately (synchronous model) — the chosen option's
        /// resolution runs later, top-level.
        /// </summary>
        void OpenRequest(Request request);

        IEntity? Get(int entityId);
        IEnumerable<IEntity> All();
        IEnumerable<IEntity> Where(TagKey<bool> marker);
    }
}
