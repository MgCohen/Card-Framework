using Framework.Tags;
using Scaffold.AppFlow;
using VContainer;
using VContainer.Unity;

namespace Framework.Bootstrap
{
    // Registers tag storage + command surface, plus the per-T apply handlers.
    // - TagRemovedHandler is non-generic; goes in as an entry point directly.
    // - TagSetHandler<T> / TagChangeHandler<T> are open generics; the visitor-based
    //   registrar drives closed-type construction at startup.
    public sealed class TagLayer : IScopeLayer
    {
        public void Install(IContainerBuilder builder)
        {
            builder.Register<TagStore>(Lifetime.Singleton);
            builder.Register<TagService>(Lifetime.Singleton);

            builder.RegisterEntryPoint<TagRemovedHandler>(Lifetime.Singleton);

            // Open-generic registration so VContainer can build closed instances on resolve.
            builder.Register(typeof(TagSetHandler<>), Lifetime.Singleton);
            builder.Register(typeof(TagChangeHandler<>), Lifetime.Singleton);

            // The registrar visits TagRegistry.All at init and resolves a closed handler
            // per T — no reflection, no MakeGenericType in our code.
            builder.RegisterEntryPoint<TagApplyHandlerRegistrar>(Lifetime.Singleton);
        }
    }
}
