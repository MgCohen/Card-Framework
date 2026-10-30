namespace Framework.Wire
{
    public sealed class BlockScope
    {
        readonly IEventEmitter _events;

        // Keep this the only block-id counter, or ids collide once a second emitter (effects) lands.
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
