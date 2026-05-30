using Framework.Wire;

namespace Framework.Tags
{
    // Sole writer for TagSetEvent<T> on both sides. Server: TagService.Set emits — this
    // handler runs synchronously and writes the store. Client: the wire delivers the
    // event — same handler, same write. Service never touches the store directly.
    internal sealed class TagSetHandler<T> : WireEventHandler<TagSetEvent<T>> where T : unmanaged
    {
        readonly TagStore _store;
        public TagSetHandler(TagStore store) { _store = store; }
        public override void Apply(TagSetEvent<T> ev) => _store.Set(ev.EntityId, ev.Slot, ev.Value);
    }
}
