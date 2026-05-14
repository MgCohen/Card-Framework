#nullable enable
using System.Collections.Generic;
using System.Linq;
using VerticalSlice.Engine.Commands;
using VerticalSlice.Engine.Entities;
using VerticalSlice.Engine.Hooks;
using VerticalSlice.Engine.Stream;
using VerticalSlice.Engine.Tags;

namespace VerticalSlice.Engine.Pipeline
{
    /// <summary>
    /// Dispatches commands. One job:
    /// <list type="number">
    /// <item>Open a Block on the wire (name = command type, payload =
    /// reflected props).</item>
    /// <item>Fire Before-phase hooks in priority order.</item>
    /// <item>Call <c>command.Apply(commandScope)</c>.</item>
    /// <item>Fire After-phase hooks in priority order.</item>
    /// <item>Close the Block.</item>
    /// </list>
    /// Owns the hook registry (<see cref="HookBus"/>). Hooks attach to
    /// commands, so subscription lives here.
    /// </summary>
    public sealed class CommandService
    {
        private readonly World _world;
        private readonly HookBus _hooks = new();
        private EffectService? _effects;   // late-bound by GameEngine to avoid ctor cycle

        public CommandService(World world) { _world = world; }

        internal void BindEffects(EffectService effects) => _effects = effects;

        public void Execute(Command command)
        {
            var (id, parentId) = _world.OpenBlock();
            _world.Log.Append(new BlockStartEvent(
                id, parentId, BlockKind.Command, command.GetType().Name,
                PayloadCapture.ExtractSource(command),
                PayloadCapture.Of(command)));
            var outcome = BlockOutcome.Completed;
            try
            {
                var hookScope = new HookScope(_world, _effects!);
                _hooks.Fire(command, HookPhase.Before, hookScope);
                command.Apply(new CommandScope(_world));
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
            public CommandScope(World w) { _world = w; }

            public Entity? Resolve(IEntity entity) => _world.Get(entity.Id);
            public Entity? Get(int id) => _world.Get(id);
            public IEnumerable<Entity> All() => _world.All();
            public IEnumerable<Entity> Where(TagKey<bool> marker) => _world.Where(marker);
        }

        private sealed class HookScope : IHookScope
        {
            private readonly World _world;
            private readonly EffectService _effects;
            public HookScope(World w, EffectService e) { _world = w; _effects = e; }

            public void Run(Effects.Effect effect) => _effects.Run(effect);
            public IEntity? Get(int id) => _world.Get(id);
            public IEnumerable<IEntity> All() => _world.All();
            public IEnumerable<IEntity> Where(TagKey<bool> marker) => _world.Where(marker);
        }
    }
}
