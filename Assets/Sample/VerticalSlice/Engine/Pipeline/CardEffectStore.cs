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
        /// Engine code (via <c>IEffectScope.FireDirect</c>) runs the
        /// returned effect through the scope so it gets the standard
        /// block-open / Validate / Run / block-close pipeline.
        /// </summary>
        internal Effect? Build<TTrigger>(IEntity card, TTrigger trigger)
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
