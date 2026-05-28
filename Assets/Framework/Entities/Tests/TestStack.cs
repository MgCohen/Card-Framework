using Framework.Tags;
using Framework.Wire;

namespace Framework.Entities.Tests
{
    // Bundles the runtime classes needed to exercise the entity + tag flow without VContainer.
    // Hand-wires the same shape EntityLayer + TagLayer would do — direct ctor calls, no DI.
    sealed class TestStack
    {
        public EventStream Stream { get; }
        public TagStore TagStore { get; }
        public TagService Tags { get; }
        public EntityStore EntityStore { get; }
        public EntityService Entities { get; }

        public TestStack() : this(new EventStream()) { }

        public TestStack(EventStream stream)
        {
            Stream = stream;
            TagStore = new TagStore();
            Tags = new TagService(TagStore, Stream);
            EntityStore = new EntityStore(Tags);
            Entities = new EntityService(EntityStore, Stream);
        }

        // Construct + initialize the non-generic apply handlers (entity create, tag remove).
        // Equivalent to what EntityLayer + TagLayer would register via EntryPoints in production.
        public void RegisterCoreHandlers()
        {
            new EntityCreatedHandler(EntityStore, Stream).InitializeAsync(default).Wait();
            new TagRemovedHandler(TagStore, Stream).InitializeAsync(default).Wait();
        }

        // Construct + initialize the closed-T tag handlers. Production uses TagApplyHandlerRegistrar
        // (visitor over TagRegistry) to drive this; tests call per-T explicitly.
        public void RegisterTagHandlers<T>() where T : unmanaged
        {
            new TagSetHandler<T>(TagStore, Stream).InitializeAsync(default).Wait();
            new TagChangeHandler<T>(TagStore, Stream).InitializeAsync(default).Wait();
        }
    }
}
