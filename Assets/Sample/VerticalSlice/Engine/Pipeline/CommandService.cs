#nullable enable
using System.Collections.Generic;
using VerticalSlice.Engine.Commands;
using VerticalSlice.Engine.Entities;
using VerticalSlice.Engine.Effects;
using VerticalSlice.Engine.Hooks;
using VerticalSlice.Engine.Stream;
using VerticalSlice.Engine.Tags;

namespace VerticalSlice.Engine.Pipeline
{
    /// <summary>
    /// The command pipeline. Per command:
    /// <list type="number">
    /// <item>Open a Block on the wire (name = command type, payload =
    /// reflected props).</item>
    /// <item>Fire Before-phase hooks in priority order.</item>
    /// <item>Call <c>command.Apply(commandScope)</c>.</item>
    /// <item>Fire After-phase hooks in priority order.</item>
    /// <item>Close the Block.</item>
    /// </list>
    /// Owns the hook registry (<see cref="HookBus"/>).
    ///
    /// <see cref="Execute"/> routes by pump state:
    /// <list type="bullet">
    /// <item>External (no active pump) → enqueue on the engine pipeline;
    /// the pump's loop will dequeue and run it, evaluating SBAs / stack
    /// between commands.</item>
    /// <item>Re-entrant (inside an Apply / Effect.Run / hook) → run inline,
    /// nests as a wire child of the current scope.</item>
    /// </list>
    /// </summary>
    public sealed class CommandService
    {
        private readonly World _world;
        private readonly HookBus _hooks = new();
        private EffectService? _effects;   // late-bound by GameEngine
        private GameEngine? _engine;       // late-bound for routing

        public CommandService(World world) { _world = world; }

        internal void BindEffects(EffectService effects) => _effects = effects;
        internal void BindEngine(GameEngine engine) => _engine = engine;

        /// <summary>
        /// Dispatch a command. From outside any active pump frame, the
        /// command rides the pipeline (FIFO with other inputs). From
        /// inside (sub-command via <c>scope.Execute</c>), it runs inline.
        /// </summary>
        public void Execute(Command command)
        {
            if (_engine!.IsPumping)
                RunInline(command);
            else
                _engine.EnqueuePipeline(command);
        }

        /// <summary>
        /// The command pipeline body — open block, hooks, Apply, hooks,
        /// close block. Always runs inline at the current wire depth.
        /// Callers: the pump's pipeline drain (top-level), re-entrant
        /// <see cref="Execute"/> from inside Apply / Effect.Run / hooks,
        /// and the engine's SBA pass (emitted commands).
        /// </summary>
        internal void RunInline(Command command, long? parentOverride = null)
        {
            var (id, parentId) = _world.OpenBlock(parentOverride);
            _world.Log.Append(new BlockStartEvent(
                id, parentId, BlockKind.Command, command.GetType().Name,
                PayloadCapture.ExtractSource(command),
                PayloadCapture.Of(command)));
            var outcome = BlockOutcome.Completed;
            try
            {
                var hookScope = new HookScope(_world, _effects!, _engine!);
                var cmdScope = new CommandScope(_world, _effects!.CardEffects, _engine!.Stack);
                _hooks.Fire(command, HookPhase.Before, hookScope);
                command.Apply(cmdScope);
                _hooks.Fire(command, HookPhase.After, hookScope);
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

        public void Subscribe<TCommand>(HookPhase phase, HookPriority priority,
                                        HookHandler<TCommand> handler)
            where TCommand : Command
            => _hooks.Subscribe(phase, priority, handler);

        // --- Internal scope implementations ----------------------------------

        private sealed class CommandScope : ICommandScope
        {
            private readonly World _world;
            public CardEffectStore CardEffects { get; }
            public Stack Stack { get; }

            public CommandScope(World w, CardEffectStore cardEffects, Stack stack)
            {
                _world = w;
                CardEffects = cardEffects;
                Stack = stack;
            }

            public Entity? Resolve(IEntity entity) => _world.Get(entity.Id);
            public Entity? Get(int id) => _world.Get(id);
            public IEnumerable<Entity> All() => _world.All();
            public IEnumerable<Entity> Where(TagKey<bool> marker) => _world.Where(marker);
        }

        private sealed class HookScope : IHookScope
        {
            private readonly World _world;
            private readonly EffectService _effects;
            private readonly GameEngine _engine;

            public HookScope(World w, EffectService e, GameEngine engine)
            {
                _world = w;
                _effects = e;
                _engine = engine;
            }

            // --- Inline (Before-phase / replacement style) -------------------

            public void Run(Effect effect) => _effects.RunInline(effect);

            public void FireDirect<TTrigger>(IEntity entity, TTrigger trigger)
                where TTrigger : class
            {
                var effect = _effects.CardEffects.Build(entity, trigger);
                if (effect != null) _effects.RunInline(effect);
            }

            // --- Stack (After-phase / reaction style) ------------------------
            // Both capture the triggering command's block id at schedule
            // time so the reaction's BlockStart carries it as ParentId
            // when the engine pops the frame — wire-level causality stays
            // visible across stack siblings.

            public void Schedule(Effect effect)
            {
                long? parent = _world.CurrentBlockId;
                _engine.Stack.Push(new StackFrame(effect, parentBlockId: parent));
            }

            public void ScheduleDirect<TTrigger>(IEntity entity, TTrigger trigger)
                where TTrigger : class
            {
                var effect = _effects.CardEffects.Build(entity, trigger);
                if (effect == null) return;
                long? parent = _world.CurrentBlockId;
                _engine.Stack.Push(new StackFrame(effect, parentBlockId: parent));
            }

            // --- Reads --------------------------------------------------------

            public IEntity? Get(int id) => _world.Get(id);
            public IEnumerable<IEntity> All() => _world.All();
            public IEnumerable<IEntity> Where(TagKey<bool> marker) => _world.Where(marker);
        }
    }
}
