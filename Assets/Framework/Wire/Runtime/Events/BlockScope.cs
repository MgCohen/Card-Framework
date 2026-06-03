namespace Framework.Wire
{
    // The sole monotonic block-id allocator and bracket emitter (server-only).
    //
    // Blocks group the mutations of one causal unit — a command today, effects/triggers later
    // — between BlockStart and BlockEnd. Allocation is centralized here by construction so that
    // when a second emitter (effects) lands, it cannot mint a colliding id: there is one counter,
    // not one per layer. A block's id is a local token threaded Open -> Close by the caller; it
    // never enters the command/handler domain (commands carry no id, L5).
    //
    // ParentId is always 0 for now: nesting (the open-block parent stack) lights up only when
    // effects run inside commands. SourceId is likewise 0 until source capture lands.
    public sealed class BlockScope
    {
        readonly IEventEmitter _events;
        int _nextId = 1;

        public BlockScope(IEventEmitter events) => _events = events;

        public int Open(BlockKind kind, int sourceId = 0)
        {
            var id = _nextId++;
            _events.Emit(new BlockStartEvent { Id = id, ParentId = 0, Kind = (byte)kind, SourceId = sourceId });
            return id;
        }

        public void Close(int id, BlockOutcome outcome = BlockOutcome.Completed)
            => _events.Emit(new BlockEndEvent { Id = id, Outcome = (byte)outcome });
    }
}
