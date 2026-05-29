using System.Collections.Generic;
using Framework.Tags;
using Framework.Wire;

namespace Framework.Entities.Tests
{
    // Bundles the runtime classes needed to exercise the entity + tag flow without VContainer.
    // Hand-wires the same shape EntityLayer + TagLayer would do — direct ctor calls.
    // Handlers accumulate via RegisterCoreHandlers / RegisterTagHandlers; one final Wire()
    // call constructs the HandlerRegistry, which subscribes to Stream.OnAppended in its ctor.
    sealed class TestStack
    {
        public EventStream Stream { get; }
        public TagStore TagStore { get; }
        public TagService Tags { get; }
        public EntityStore EntityStore { get; }
        public EntityService Entities { get; }
        public HandlerRegistry Registry { get; private set; }

        readonly List<IWireEventHandler> _handlers = new();

        public TestStack() : this(new EventStream()) { }

        public TestStack(EventStream stream)
        {
            Stream = stream;
            TagStore = new TagStore();
            Tags = new TagService(TagStore, Stream);
            EntityStore = new EntityStore();
            Entities = new EntityService(EntityStore, Stream);
        }

        public void RegisterCoreHandlers()
        {
            _handlers.Add(new EntityCreatedHandler(EntityStore));
            _handlers.Add(new TagRemovedHandler(TagStore));
        }

        public void RegisterTagHandlers<T>() where T : unmanaged
        {
            _handlers.Add(new TagSetHandler<T>(TagStore));
            _handlers.Add(new TagChangeHandler<T>(TagStore));
        }

        public void Wire() => Registry = new HandlerRegistry(Stream, _handlers);
    }
}
