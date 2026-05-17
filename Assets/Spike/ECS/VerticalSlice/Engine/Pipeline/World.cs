#nullable enable
using System.Collections.Generic;
using VerticalSlice.Engine.Stream;

namespace VerticalSlice.Engine.Pipeline
{
    /// <summary>
    /// Pure substrate. Owns the event log and the block-id allocator
    /// (the global "what's currently open" stack used by
    /// <see cref="EffectService"/> / <see cref="CommandService"/> when
    /// they emit <see cref="BlockStartEvent"/> / <see cref="BlockEndEvent"/>
    /// pairs). <b>No verbs.</b> Entity storage + queries live on
    /// <see cref="EntityStore"/>; effect / command dispatch live on
    /// dedicated services.
    ///
    /// World is intentionally a god-free zone: anyone with a reference
    /// can read the log and the current block id, but nothing on this
    /// surface mutates entities, fires effects, or dispatches commands.
    /// Authors writing inside effects / commands / hooks reach scoped
    /// views (<c>IEffectScope</c>, <c>ICommandScope</c>) that the
    /// services hand them.
    /// </summary>
    public sealed class World
    {
        public EventLog Log { get; } = new();

        // Block-id allocator + open-block stack. Shared by EffectService
        // and CommandService so block ids are globally monotonic and
        // parent ids correctly point at whichever block is currently
        // open above us.
        private long _nextBlockId = 1;
        private readonly Stack<long> _blockStack = new();

        public long? CurrentBlockId => _blockStack.Count > 0 ? _blockStack.Peek() : null;

        /// <summary>
        /// Open a block. By default the parent id comes from the current
        /// open-block stack (so nested dispatches form a tree). An optional
        /// <paramref name="parentOverride"/> lets stack-scheduled reactions
        /// thread their causal parent on the wire even though they resolve
        /// at depth-0 (sibling of the triggering frame) — the triggering
        /// block id is captured at <c>Schedule</c> time and passed through
        /// here when the reaction eventually resolves.
        /// </summary>
        internal (long id, long? parentId) OpenBlock(long? parentOverride = null)
        {
            long? parent = parentOverride
                ?? (_blockStack.Count > 0 ? _blockStack.Peek() : null);
            long id = _nextBlockId++;
            _blockStack.Push(id);
            return (id, parent);
        }

        internal long CloseBlock() => _blockStack.Pop();
    }
}
