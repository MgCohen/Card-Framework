#nullable enable
using System;
using System.Collections.Generic;
using VerticalSlice.Engine.Effects;
using VerticalSlice.Engine.Entities;

namespace VerticalSlice.Engine.Pipeline
{
    /// <summary>
    /// Holds direct-trigger effects attached to entities. Keyed by
    /// (entity id, trigger type) → factory that builds the effect with
    /// the trigger payload at fire time.
    ///
    /// Authors don't reach into this directly. They attach via
    /// <c>cardEffects.Set&lt;Deathrattle&gt;(card, t =&gt; new …(t))</c> at
    /// setup, and effects fire via <c>IEffectScope.FireDirect</c> from
    /// engine bridges. The store stays out of the effect-body API.
    /// </summary>
    public sealed class CardEffectStore
    {
        private readonly Dictionary<(int id, Type triggerType), object> _factories = new();

        public void Set<TTrigger>(IEntity card, Func<TTrigger, Effect> factory)
            where TTrigger : class
        {
            _factories[(card.Id, typeof(TTrigger))] = factory;
        }

        public void Clear<TTrigger>(IEntity card) where TTrigger : class
        {
            _factories.Remove((card.Id, typeof(TTrigger)));
        }

        public bool Has<TTrigger>(IEntity card) where TTrigger : class
            => _factories.ContainsKey((card.Id, typeof(TTrigger)));

        /// <summary>
        /// Build and return the effect attached to <paramref name="card"/>
        /// for trigger <typeparamref name="TTrigger"/>, bound to
        /// <paramref name="trigger"/>. Returns null if no such effect.
        /// Used by:
        /// <list type="bullet">
        /// <item>Engine bridges (via <c>IEffectScope.FireDirect</c> /
        /// <c>IHookScope.ScheduleDirect</c>) — fire triggered effects
        /// attached to a card on death / move / etc.</item>
        /// <item>Game-side commands (e.g. <c>PlayCard.Apply</c>) that
        /// look up the card's <c>OnCast</c> factory and push the result
        /// onto the engine stack.</item>
        /// </list>
        /// </summary>
        public Effect? Build<TTrigger>(IEntity card, TTrigger trigger)
            where TTrigger : class
        {
            if (_factories.TryGetValue((card.Id, typeof(TTrigger)), out var raw)
                && raw is Func<TTrigger, Effect> factory)
            {
                return factory(trigger);
            }
            return null;
        }
    }
}
