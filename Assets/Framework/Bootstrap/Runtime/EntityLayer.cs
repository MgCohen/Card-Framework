using Framework.Entities;
using Scaffold.AppFlow;
using VContainer;

namespace Framework.Bootstrap
{
    public sealed class EntityLayer : IScopeLayer
    {
        public void Install(IContainerBuilder builder)
        {
            builder.Register<EntityStore>(Lifetime.Singleton);
            builder.Register<EntityService>(Lifetime.Singleton);
            builder.RegisterHandler<EntityCreatedHandler>();
        }
    }
}
