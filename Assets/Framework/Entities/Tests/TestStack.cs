using System.Collections.Generic;
using Framework.Tags;
using Framework.Wire;

namespace Framework.Entities.Tests
{
    // Hand-wires the same shape EntityLayer + TagLayer would. Auto-registers the standard
    // handler set (Entity + Tag<int> + Tag<bool>) and constructs the HandlerRegistry, so the
    // service-emit path has a live writer on the other end. Tests that exercise the
    // service-emit path use this; tests that hit the store directly can construct a bare
    // EntityStore / TagStore instead.
    sealed class TestStack
    {
        public EventStream Stream { get; }
        public TagStore TagStore { get; }
        public TagService Tags { get; }
        public EntityStore EntityStore { get; }
        public EntityTypeRegistry EntityTypes { get; }
        public EntityService Entities { get; }
        public HandlerRegistry Registry { get; }

        public TestStack() : this(new EventStream()) { }

        public TestStack(EventStream stream)
        {
            Stream = stream;
            TagStore = new TagStore();
            Tags = new TagService(TagStore, Stream);
            EntityStore = new EntityStore();
            EntityTypes = EntityTypeRegistry.WithBaselines();
            Entities = new EntityService(EntityStore, Stream);

            var handlers = new List<IWireEventHandler>
            {
                new EntityCreatedHandler(EntityStore, EntityTypes),
                new TagRemovedHandler(TagStore),
                new TagSetHandler<int>(TagStore),
                new TagChangeHandler<int>(TagStore),
                new TagSetHandler<bool>(TagStore),
                new TagChangeHandler<bool>(TagStore),
            };
            Registry = new HandlerRegistry(Stream, handlers);
        }
    }
}
