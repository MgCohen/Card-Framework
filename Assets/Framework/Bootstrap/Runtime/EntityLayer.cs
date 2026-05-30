using Framework.Entities;
using Scaffold.AppFlow;
using VContainer;

namespace Framework.Bootstrap
{
    // EntityStore + EntityCreatedHandler register on both sides — both need to read /
    // reconstruct entity state. EntityService — the command surface — is server-only;
    // the client never allocates ids or authors entities.
    public sealed class EntityLayer : IScopeLayer
    {
        readonly bool _isServer;

        public EntityLayer(bool isServer) { _isServer = isServer; }

        public void Install(IContainerBuilder builder)
        {
            builder.Register<EntityStore>(Lifetime.Singleton);
            if (_isServer) builder.Register<EntityService>(Lifetime.Singleton);
            builder.RegisterHandler<EntityCreatedHandler>();
        }
    }
}
