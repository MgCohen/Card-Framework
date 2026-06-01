using Framework.Wire;

namespace Framework.Tags
{
    internal sealed class TagSetHandler<T> : WireEventHandler<TagSetEvent<T>> where T : unmanaged
    {
        readonly TagStore _store;
        public TagSetHandler(TagStore store) { _store = store; }
        public override void Apply(TagSetEvent<T> ev) => _store.Set(ev.EntityId, ev.Slot, ev.Value);
    }
}
