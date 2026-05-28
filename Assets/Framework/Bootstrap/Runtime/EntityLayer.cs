using Framework.Entities;
using Scaffold.AppFlow;
using VContainer;
using VContainer.Unity;

namespace Framework.Bootstrap
{
    // Registers entity storage + command surface + the entity-create apply handler.
    // Same registrations both sides — asymmetry lives in EngineLayer's bridge choice.
    public sealed class EntityLayer : IScopeLayer
    {
        public void Install(IContainerBuilder builder)
        {
            builder.Register<EntityStore>(Lifetime.Singleton);
            builder.Register<EntityService>(Lifetime.Singleton);
            builder.RegisterEntryPoint<EntityCreatedHandler>(Lifetime.Singleton);
        }
    }
}
