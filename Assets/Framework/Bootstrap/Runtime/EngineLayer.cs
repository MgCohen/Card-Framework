using Framework.Engine;
using Framework.Wire;
using Scaffold.AppFlow;
using VContainer;
using VContainer.Unity;

namespace Framework.Bootstrap
{
    // Engine substrate + the side-asymmetric wire bridge. EntityLayer and TagLayer compose
    // on top — they don't fork by side because the asymmetry is fully captured here.
    public sealed class EngineLayer : IScopeLayer
    {
        readonly bool _isServer;

        public EngineLayer(bool isServer) { _isServer = isServer; }

        public void Install(IContainerBuilder builder)
        {
            builder.Register<NullTransport>(Lifetime.Singleton).As<ITransport>();
            builder.Register<Network>(c => new Network(c.Resolve<ITransport>()), Lifetime.Singleton);

            builder.Register<EventStream>(Lifetime.Singleton)
                .AsSelf()
                .As<IEventEmitter>()
                .As<IHandlerRegistry>();

            builder.Register<GameEngine>(Lifetime.Singleton);

            if (_isServer) builder.RegisterEntryPoint<WireForwarder>(Lifetime.Singleton);
            else           builder.RegisterEntryPoint<NetworkInboundBridge>(Lifetime.Singleton);
        }
    }
}
