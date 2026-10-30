using Framework.Entities;
using VContainer;

namespace Framework.Bootstrap
{
    public sealed class EntityInstaller : IInstaller
    {
        readonly bool _isServer;

        public EntityInstaller(bool isServer) { _isServer = isServer; }

        public void Install(IContainerBuilder builder)
        {
            builder.Register<EntityStore>(Lifetime.Singleton);
            builder.RegisterInstance(EntityTypeRegistry.WithBaselines());
            if (_isServer) builder.Register<EntityService>(Lifetime.Singleton);
            builder.RegisterHandler<EntityCreatedHandler>();
        }
    }
}
