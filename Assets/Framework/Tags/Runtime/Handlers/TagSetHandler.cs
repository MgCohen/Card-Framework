using Framework.Wire;

namespace Framework.Tags
{
    // Mirror-side writer for TagSetEvent<T>. Depends on TagStore only — no IEventEmitter,
    // no TagService reference. Type system enforces no-bounce: this handler cannot re-emit.
    internal sealed class TagSetHandler<T> : WireEventHandler<TagSetEvent<T>> where T : unmanaged
    {
        readonly TagStore _store;
        public TagSetHandler(TagStore store) { _store = store; }
        public override void Apply(TagSetEvent<T> ev) => _store.Set(ev.EntityId, ev.Slot, ev.Value);
    }
}
