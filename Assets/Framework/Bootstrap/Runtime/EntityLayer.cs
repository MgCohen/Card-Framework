using Framework.Entities;
using VContainer;

namespace Framework.Bootstrap
{
    public sealed class EntityLayer : BootstrapLayer
    {
        public EntityLayer(bool isServer) : base(isServer) { }

        public override void Install(IContainerBuilder builder)
        {
            builder.Register<EntityStore>(Lifetime.Singleton);
            builder.RegisterInstance(EntityTypeRegistry.WithBaselines());
            if (_isServer) builder.Register<EntityService>(Lifetime.Singleton);
            builder.RegisterHandler<EntityCreatedHandler>();
        }
    }
}
