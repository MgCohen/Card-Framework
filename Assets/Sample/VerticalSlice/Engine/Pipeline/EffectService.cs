#nullable enable
using System.Collections.Generic;
using VerticalSlice.Engine.Commands;
using VerticalSlice.Engine.Effects;
using VerticalSlice.Engine.Entities;
using VerticalSlice.Engine.Stream;
using VerticalSlice.Engine.Tags;

namespace VerticalSlice.Engine.Pipeline
{
    /// <summary>
    /// Runs effects. One job:
    /// <list type="number">
    /// <item>Open a Block on the wire (name = effect type, payload =
    /// reflected props).</item>
    /// <item>Call <c>effect.Run(effectScope)</c>.</item>
    /// <item>Close the Block.</item>
    /// </list>
    /// No hook firing on effects in the slice — hooks attach to commands.
    /// Effect-level reactions (e.g. "after Fireball, draw a card") would be
    /// an additive change here later.
    /// </summary>
    public sealed class EffectService
    {
        private readonly World _world;
        private readonly CommandService _commands;

        public EffectService(World world, CommandService commands)
        {
            _world = world;
            _commands = commands;
        }

        public void Run(Effect effect)
        {
            var (id, parentId) = _world.OpenBlock();
            _world.Log.Append(new BlockStartEvent(
                id, parentId, BlockKind.Effect, effect.GetType().Name,
                PayloadCapture.ExtractSource(effect),
                PayloadCapture.Of(effect)));
            var outcome = BlockOutcome.Completed;
            try
            {
                effect.Run(new EffectScope(_world, _commands));
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
            private readonly World _world;
            private readonly CommandService _commands;
            public EffectScope(World w, CommandService c) { _world = w; _commands = c; }

            public void Execute(Command command) => _commands.Execute(command);
            public IEntity? Get(int id) => _world.Get(id);
            public IEnumerable<IEntity> All() => _world.All();
            public IEnumerable<IEntity> Where(TagKey<bool> marker) => _world.Where(marker);
        }
    }
}
