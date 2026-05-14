#nullable enable
using System;
using System.Collections.Generic;
using VerticalSlice.Engine.Commands;
using VerticalSlice.Engine.Effects;
using VerticalSlice.Engine.Entities;
using VerticalSlice.Engine.Hooks;
using VerticalSlice.Engine.Stream;
using VerticalSlice.Engine.Tags;

namespace VerticalSlice.Engine.Pipeline
{
    /// <summary>
    /// Runs effects. Per dispatch:
    /// <list type="number">
    /// <item>Build the effect's scope.</item>
    /// <item>Call <c>effect.Validate(scope)</c>. If false, abort — no
    /// block emitted, no wire artifact.</item>
    /// <item>Open a Block on the wire.</item>
    /// <item>Call <c>effect.Run(scope)</c>.</item>
    /// <item>Close the Block.</item>
    /// </list>
    ///
    /// Also provides <see cref="Register{TCommand}"/> for hook effects
    /// (triggered effects subscribed to a command type) — the factory
    /// receives the in-flight command and produces an Effect bound to it.
    /// </summary>
    public sealed class EffectService
    {
        private readonly World _world;
        private readonly CommandService _commands;
        public CardEffectStore CardEffects { get; }

        public EffectService(World world, CommandService commands, CardEffectStore cardEffects)
        {
            _world = world;
            _commands = commands;
            CardEffects = cardEffects;
        }

        public void Run(Effect effect)
        {
            var scope = new EffectScope(_world, _commands, CardEffects, this);

            // Validate is a separate step — short-circuits before any wire
            // event is emitted. "Did the effect run" == "did we get past
            // this check + into the block."
            if (!effect.Validate(scope)) return;

            var (id, parentId) = _world.OpenBlock();
            _world.Log.Append(new BlockStartEvent(
                id, parentId, BlockKind.Effect, effect.GetType().Name,
                PayloadCapture.ExtractSource(effect),
                PayloadCapture.Of(effect)));
            var outcome = BlockOutcome.Completed;
            try
            {
                effect.Run(scope);
            }
            catch
            {
                outcome = BlockOutcome.Errored;
                throw;
            }
            finally
            {
                var closingId = _world.CloseBlock();
                _world.Log.Append(new BlockEndEvent(closingId, outcome));
            }
        }

        /// <summary>
        /// Register a hook effect — bind a factory to a command type so
        /// that, whenever a command of that type fires at the given phase /
        /// priority, the factory is called with the in-flight command,
        /// the resulting effect is dispatched through <see cref="Run"/>
        /// (with Validate gating it). One call per registration; the
        /// effect's class doesn't have to know the bus exists.
        /// </summary>
        public void Register<TCommand>(HookPhase phase, HookPriority priority,
                                       Func<TCommand, Effect> factory)
            where TCommand : Command
        {
            _commands.Subscribe<TCommand>(phase, priority, (cmd, _) =>
            {
                var effect = factory(cmd);
                Run(effect);
            });
        }

        // --- Internal scope ---------------------------------------------------

        private sealed class EffectScope : IEffectScope
        {
            private readonly World _world;
            private readonly CommandService _commands;
            private readonly CardEffectStore _cardEffects;
            private readonly EffectService _effects;

            public EffectScope(World w, CommandService c, CardEffectStore cardEffects,
                               EffectService effects)
            {
                _world = w;
                _commands = c;
                _cardEffects = cardEffects;
                _effects = effects;
            }

            public void Execute(Command command) => _commands.Execute(command);

            public void Run(Effect effect) => _effects.Run(effect);

            public void FireDirect<TTrigger>(IEntity entity, TTrigger trigger)
                where TTrigger : class
            {
                var effect = _cardEffects.Build(entity, trigger);
                if (effect != null) _effects.Run(effect);
            }

            public IEntity? Get(int id) => _world.Get(id);
            public IEnumerable<IEntity> All() => _world.All();
            public IEnumerable<IEntity> Where(TagKey<bool> marker) => _world.Where(marker);
        }
    }
}
