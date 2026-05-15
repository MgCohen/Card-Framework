#nullable enable
using System;
using System.Collections.Generic;
using VerticalSlice.Engine.Commands;
using VerticalSlice.Engine.Effects;
using VerticalSlice.Engine.Entities;

namespace VerticalSlice.Engine.Pipeline
{
    /// <summary>
    /// Single source of truth for "what runs when X happens." Holds two
    /// kinds of bindings:
    ///
    /// <list type="bullet">
    /// <item><b>Command hooks</b> — <see cref="Subscribe{TCommand}"/> binds
    /// a <see cref="HookConfig"/> + Effect factory to a command type.
    /// When that command fires, the engine iterates priority-sorted hooks
    /// for the phase, calls each config's <see cref="HookConfig.IsActive"/>
    /// pre-filter, builds the effect via the factory, and dispatches.</item>
    /// <item><b>Direct triggers</b> — <see cref="SetDirect{TTrigger}"/>
    /// binds a per-entity factory keyed by trigger type. Used by play /
    /// trigger commands and by hook bodies (Deathrattle bridge) that need
    /// to "fire this entity's effect for this trigger." Looked up via
    /// <see cref="BuildDirect{TTrigger}"/>.</item>
    /// </list>
    ///
    /// <para>The two registration paths and the two lookup paths fold into
    /// one store so subscription state isn't split across services. Game
    /// code only needs to know this one type to express anything that
    /// reacts to engine activity.</para>
    /// </summary>
    public sealed class EffectStore
    {
        private readonly Dictionary<(Type, HookPhase), List<Subscription>> _hooks = new();
        private readonly Dictionary<(int entityId, Type triggerType), object> _direct = new();

        // --- Command hooks ---------------------------------------------------

        /// <summary>
        /// Subscribe an Effect factory to a command type. Phase / priority /
        /// activation gate are bundled in <paramref name="config"/>. At fire
        /// time the engine matches by (command type, phase), iterates by
        /// ascending priority, calls <see cref="HookConfig.IsActive"/>, and
        /// — if active — invokes the factory and dispatches the resulting
        /// effect through the stack.
        /// </summary>
        public Subscription Subscribe<TCommand>(HookConfig config, Func<TCommand, Effect> factory)
            where TCommand : Command
        {
            var key = (typeof(TCommand), config.Phase);
            if (!_hooks.TryGetValue(key, out var list))
            {
                list = new List<Subscription>();
                _hooks[key] = list;
            }
            var sub = new Subscription(config, cmd => factory((TCommand)cmd));
            list.Add(sub);
            list.Sort((a, b) => a.Config.Priority.CompareTo(b.Config.Priority));
            return sub;
        }

        /// <summary>Remove a previously registered hook subscription.</summary>
        public void Unsubscribe(Subscription sub)
        {
            foreach (var list in _hooks.Values) list.Remove(sub);
        }

        /// <summary>
        /// Snapshot of subscribers matching <paramref name="commandType"/> +
        /// <paramref name="phase"/>, priority-sorted. Snapshot semantics so
        /// handlers may register or remove subs mid-iteration without
        /// disturbing the in-flight dispatch.
        /// </summary>
        internal IReadOnlyList<Subscription> Subscribers(Type commandType, HookPhase phase)
        {
            if (_hooks.TryGetValue((commandType, phase), out var list))
                return list.ToArray();
            return Array.Empty<Subscription>();
        }

        // --- Direct triggers -------------------------------------------------

        /// <summary>
        /// Attach an Effect factory to a specific entity, keyed by trigger
        /// type. The factory is later invoked by
        /// <see cref="BuildDirect{TTrigger}"/> (used by play commands and
        /// hook bodies that fire per-entity triggers — Deathrattle, OnCast).
        /// Re-setting on the same (entity, trigger) replaces.
        /// </summary>
        public void SetDirect<TTrigger>(IEntity entity, Func<TTrigger, Effect> factory)
            where TTrigger : class
        {
            _direct[(entity.Id, typeof(TTrigger))] = factory;
        }

        public void ClearDirect<TTrigger>(IEntity entity) where TTrigger : class
        {
            _direct.Remove((entity.Id, typeof(TTrigger)));
        }

        public bool HasDirect<TTrigger>(IEntity entity) where TTrigger : class
            => _direct.ContainsKey((entity.Id, typeof(TTrigger)));

        /// <summary>
        /// Look up and build the effect for <paramref name="entity"/> +
        /// <typeparamref name="TTrigger"/>, bound to <paramref name="trigger"/>.
        /// Returns null if nothing is registered.
        /// </summary>
        public Effect? BuildDirect<TTrigger>(IEntity entity, TTrigger trigger)
            where TTrigger : class
        {
            if (_direct.TryGetValue((entity.Id, typeof(TTrigger)), out var raw)
                && raw is Func<TTrigger, Effect> factory)
            {
                return factory(trigger);
            }
            return null;
        }
    }

    /// <summary>
    /// Opaque handle for a hook subscription. Returned by
    /// <see cref="EffectStore.Subscribe{TCommand}"/>; pass back to
    /// <see cref="EffectStore.Unsubscribe"/> to remove. Also carries the
    /// engine-internal dispatch state.
    /// </summary>
    public sealed class Subscription
    {
        public HookConfig Config { get; }
        internal Func<Command, Effect> Factory { get; }

        internal Subscription(HookConfig config, Func<Command, Effect> factory)
        {
            Config = config;
            Factory = factory;
        }
    }
}
