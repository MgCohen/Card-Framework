using Framework.Wire;

namespace Framework.Tags
{
    internal sealed class TagChangeHandler<T> : WireEventHandler<TagChangeEvent<T>> where T : unmanaged
    {
        readonly TagStore _store;
        public TagChangeHandler(TagStore store) { _store = store; }
        public override void Apply(TagChangeEvent<T> ev) => _store.Set(ev.EntityId, ev.Slot, ev.Next);
    }
}
