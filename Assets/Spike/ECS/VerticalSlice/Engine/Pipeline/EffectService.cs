#nullable enable
using System;
using System.Collections.Generic;
using VerticalSlice.Engine.Commands;
using VerticalSlice.Engine.Effects;
using VerticalSlice.Engine.Entities;
using VerticalSlice.Engine.Stream;
using VerticalSlice.Engine.Tags;

namespace VerticalSlice.Engine.Pipeline
{
    /// <summary>
    /// Resolves effects. <b>No external dispatch surface</b> — effects can
    /// only enter the engine via:
    /// <list type="bullet">
    /// <item>Being popped from <see cref="Stack"/> by the engine pump.</item>
    /// <item>Being run inline from inside another effect's body via
    /// <c>IEffectScope.Run</c>.</item>
    /// </list>
    /// External callers wishing to "cast" an effect use a command (e.g.
    /// <c>PlayCard</c>) that pushes onto the stack.
    ///
    /// <para>Per effect resolution: build scope, call <c>Validate</c>
    /// (abort if false — no block emitted), open a Block, call <c>Run</c>
    /// (body may dispatch sub-commands inline via
    /// <see cref="IEffectScope.Execute"/>), close the Block.</para>
    /// </summary>
    public sealed class EffectService
    {
        private readonly World _world;
        private readonly CommandService _commands;
        private EntityStore? _entities;
        private EffectStore? _store;
        private RequestsService? _requests;
        private GameEngine? _engine;

        public EffectService(World world, CommandService commands)
        {
            _world = world;
            _commands = commands;
        }

        internal void BindEntities(EntityStore entities) => _entities = entities;
        internal void BindStore(EffectStore store) => _store = store;
        internal void BindRequests(RequestsService requests) => _requests = requests;
        internal void BindEngine(GameEngine engine) => _engine = engine;

        /// <summary>
        /// Build a fresh scope of the shape effects see. Used by
        /// <see cref="CommandService"/> when dispatching hooks (the scope
        /// is read-only-ish from the hook config's perspective) and by
        /// the engine's SBA pass.
        /// </summary>
        internal IEffectScope NewScope()
            => new EffectScope(_entities!, _commands, _store!, this, _requests!, _engine!);

        /// <summary>
        /// Run a request-resolution body inline. The body's
        /// <c>scope.Execute</c> / <c>scope.Run</c> calls route through
        /// <see cref="CommandService.Execute"/> / <see cref="RunInline"/>
        /// and nest under whatever block is currently open (typically
        /// none — Respond is an external entry like Enqueue). After the
        /// body finishes we kick the pump so any stack frames it pushed
        /// drain naturally.
        /// </summary>
        internal void RunResolution(Action<IEffectScope> body)
        {
            var scope = (EffectScope)NewScope();
            body(scope);
            _engine!.Kick();
        }

        /// <summary>
        /// Resolve an effect inline at the current wire depth — open
        /// block, run body, close. Callers: the Stack's
        /// <c>ResolveAsync</c> when popping a frame; <c>IEffectScope.Run</c>
        /// for sub-effect composition; <c>FireDirect</c>.
        ///
        /// <paramref name="parentOverride"/> lets the Stack carry the
        /// triggering block's id onto a resolved effect's <c>BlockStart</c>,
        /// so After-phase reactions (which open at world depth 0) still
        /// thread back to the command that scheduled them.
        /// </summary>
        internal void RunInline(Effect effect, long? parentOverride = null)
        {
            var scope = NewScope();

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

        // --- Internal scope ---------------------------------------------------

        private sealed class EffectScope : IEffectScope
        {
            private readonly EntityStore _entities;
            private readonly CommandService _commands;
            private readonly EffectStore _store;
            private readonly EffectService _effects;
            private readonly RequestsService _requests;
            private readonly GameEngine _engine;

            public EffectScope(EntityStore entities, CommandService c, EffectStore store,
                               EffectService effects, RequestsService requests,
                               GameEngine engine)
            {
                _entities = entities;
                _commands = c;
                _store = store;
                _effects = effects;
                _requests = requests;
                _engine = engine;
            }

            // Sub-command from inside an effect's body — runs inline,
            // nests as a wire child of the effect's block.
            public void Execute(Command command) => _commands.Execute(command);

            // Sub-effect — runs inline. Nests under the current block.
            public void Run(Effect effect) => _effects.RunInline(effect);

            public void FireDirect<TTrigger>(IEntity entity, TTrigger trigger)
                where TTrigger : class
            {
                var effect = _store.BuildDirect(entity, trigger);
                if (effect != null) _effects.RunInline(effect);
            }

            public bool HasDirect<TTrigger>(IEntity entity) where TTrigger : class
                => _store.HasDirect<TTrigger>(entity);

            public void OpenRequest(Request request) => _requests.OpenRequestInternal(request);

            public IEntity? Get(int id) => _entities.Get(id);

            public EntityQuery With(TagKey<bool> marker) => _entities.With(marker);
            public EntityQuery Where<T>(TagKey<T> key, T value) where T : notnull
                => _entities.Where(key, value);
        }
    }
}
