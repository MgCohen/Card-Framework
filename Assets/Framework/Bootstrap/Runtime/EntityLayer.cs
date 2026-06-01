using Framework.Entities;
using Scaffold.AppFlow;
using VContainer;

namespace Framework.Bootstrap
{
    public sealed class EntityLayer : IScopeLayer
    {
        readonly bool _isServer;

        public EntityLayer(bool isServer) { _isServer = isServer; }

        public void Install(IContainerBuilder builder)
        {
            builder.Register<EntityStore>(Lifetime.Singleton);
            builder.RegisterInstance(EntityTypeRegistry.WithBaselines());
            if (_isServer) builder.Register<EntityService>(Lifetime.Singleton);
            builder.RegisterHandler<EntityCreatedHandler>();
        }
    }
}
