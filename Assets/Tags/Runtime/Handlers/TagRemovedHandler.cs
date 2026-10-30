using Framework.Wire;

namespace Framework.Tags
{
    internal sealed class TagRemovedHandler : WireEventHandler<TagRemovedEvent>
    {
        readonly TagStore _store;
        public TagRemovedHandler(TagStore store) { _store = store; }
        public override void Apply(TagRemovedEvent ev) => _store.Remove(ev.EntityId, ev.Slot);
    }
}
