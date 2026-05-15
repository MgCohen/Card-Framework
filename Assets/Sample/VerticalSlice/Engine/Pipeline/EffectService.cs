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
    /// Resolves effects. <b>No external dispatch surface</b> — effects can
    /// only enter the engine via:
    /// <list type="bullet">
    /// <item>Being popped from <see cref="Stack"/> by
    /// <see cref="GameEngine.Evaluate"/>.</item>
    /// <item>Being inline-run from within a resolving frame via
    /// <c>IHookScope.Run</c> (Before-phase replacement) or
    /// <c>IEffectScope.Run</c> (sub-effect composition).</item>
    /// </list>
    /// External callers wishing to "cast" an effect use a command (e.g.
    /// <c>PlayCard</c>) that pushes onto the stack via <see cref="Stack.Push"/>.
    ///
    /// Per effect resolution:
    /// <list type="number">
    /// <item>Build the effect's scope.</item>
    /// <item>Call <c>Validate</c>. If false, abort — no block emitted.</item>
    /// <item>Open a Block on the wire (the effect's scope).</item>
    /// <item>Call <c>Run(scope)</c> — body may dispatch sub-commands inline
    /// via <see cref="IEffectScope.Execute"/>.</item>
    /// <item>Close the Block.</item>
    /// </list>
    ///
    /// <see cref="Register{TCommand}"/> binds a hook effect to a command
    /// type. Phase determines the dispatch mode: <c>Before</c> runs the
    /// effect inline (replacement-style), <c>After</c> schedules it onto
    /// the engine stack (reaction-style, becomes its own top-level frame).
    /// </summary>
    public sealed class EffectService
    {
        private readonly World _world;
        private readonly CommandService _commands;
        private RequestsService? _requests;
        private GameEngine? _engine;
        public CardEffectStore CardEffects { get; }

        public EffectService(World world, CommandService commands, CardEffectStore cardEffects)
        {
            _world = world;
            _commands = commands;
            CardEffects = cardEffects;
        }

        internal void BindRequests(RequestsService requests) => _requests = requests;
        internal void BindEngine(GameEngine engine) => _engine = engine;

        /// <summary>
        /// Build a fresh scope of the shape effects see. Used by the
        /// engine's SBA pass to pass a read-only context to each
        /// <see cref="ISBA.Evaluate"/> — SBAs use Get/All/Where to walk
        /// world state and yield commands. SBAs must not call the
        /// mutating methods (Execute / Run / OpenRequest); they only
        /// emit commands the engine then runs inline.
        /// </summary>
        internal IEffectScope NewScope()
            => new EffectScope(_world, _commands, CardEffects, this, _requests!, _engine!);

        /// <summary>
        /// Run a request-resolution body that needs a scope but should
        /// NOT open its own block on the wire. Used by
        /// <c>RequestsService.Respond</c>: commands / effects the body
        /// dispatches appear directly on the wire, not wrapped in a
        /// synthetic "RequestResolution" block.
        ///
        /// Wraps the body in a pump frame: the body's
        /// <c>scope.Execute</c> / <c>scope.Run</c> calls all see
        /// <see cref="GameEngine.IsPumping"/> = true and route inline,
        /// keeping the resolution cohesive (SBAs drain once at the end,
        /// not between each command in the body).
        /// </summary>
        internal void RunResolution(Action<IEffectScope> body)
        {
            var scope = new EffectScope(_world, _commands, CardEffects, this, _requests!, _engine!);
            _engine!.EnterPumpFrame(() => body(scope));
        }

        /// <summary>
        /// Resolve an effect inline at the current wire depth — open
        /// block, run body, close. Callers: the engine's stack-pop step,
        /// <c>IHookScope.Run</c> (Before-phase replacement),
        /// <c>IEffectScope.Run</c> (sub-effect composition),
        /// <c>FireDirect</c>.
        ///
        /// <paramref name="parentOverride"/> lets the engine carry the
        /// triggering block's id onto a stack-resolved effect's
        /// <c>BlockStart</c>, even though that effect opens at world
        /// depth 0.
        /// </summary>
        internal void RunInline(Effect effect, long? parentOverride = null)
        {
            var scope = new EffectScope(_world, _commands, CardEffects, this, _requests!, _engine!);

            // Validate gates before any wire event. "Did the effect run"
            // == "did we get past this check + into the block."
            if (!effect.Validate(scope)) return;

            var (id, parentId) = _world.OpenBlock(parentOverride);
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
        /// priority, the factory is called with the in-flight command and
        /// the resulting effect is dispatched. <b>Phase determines the
        /// dispatch mode</b>:
        /// <list type="bullet">
        /// <item><c>Before</c> → inline (the effect's body completes
        /// before <c>Apply</c> runs; for replacement-style mechanics like
        /// DivineShield zeroing damage).</item>
        /// <item><c>After</c> → schedule on the stack (the effect resolves
        /// as its own top-level frame after the in-flight command
        /// finishes; for reaction-style mechanics like Backlash).</item>
        /// </list>
        /// </summary>
        public void Register<TCommand>(HookPhase phase, HookPriority priority,
                                       Func<TCommand, Effect> factory)
            where TCommand : Command
        {
            _commands.Subscribe<TCommand>(phase, priority, (cmd, _) =>
            {
                var effect = factory(cmd);
                if (phase == HookPhase.Before)
                {
                    RunInline(effect);
                }
                else
                {
                    long? triggerBlockId = _world.CurrentBlockId;
                    _engine!.Stack.Push(new StackFrame(effect, parentBlockId: triggerBlockId));
                }
            });
        }

        // --- Internal scope ---------------------------------------------------

        private sealed class EffectScope : IEffectScope
        {
            private readonly World _world;
            private readonly CommandService _commands;
            private readonly CardEffectStore _cardEffects;
            private readonly EffectService _effects;
            private readonly RequestsService _requests;
            private readonly GameEngine _engine;

            public EffectScope(World w, CommandService c, CardEffectStore cardEffects,
                               EffectService effects, RequestsService requests,
                               GameEngine engine)
            {
                _world = w;
                _commands = c;
                _cardEffects = cardEffects;
                _effects = effects;
                _requests = requests;
                _engine = engine;
            }

            // Sub-command from inside an effect's body — runs inline,
            // nests as a wire child of the effect's block.
            public void Execute(Command command) => _commands.Execute(command);

            // Sub-effect — runs inline. Whether we got here from a
            // stack-resolving frame or from inside a hook, IsPumping is
            // true and nesting is correct.
            public void Run(Effect effect) => _effects.RunInline(effect);

            public void FireDirect<TTrigger>(IEntity entity, TTrigger trigger)
                where TTrigger : class
            {
                var effect = _cardEffects.Build(entity, trigger);
                if (effect != null) _effects.RunInline(effect);
            }

            public void OpenRequest(Request request) => _requests.OpenRequestInternal(request);

            public IEntity? Get(int id) => _world.Get(id);
            public IEnumerable<IEntity> All() => _world.All();
            public IEnumerable<IEntity> Where(TagKey<bool> marker) => _world.Where(marker);
        }
    }
}
