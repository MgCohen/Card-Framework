using Framework.Engine;
using Framework.Wire;
using VContainer;

namespace Framework.Bootstrap
{
    public sealed class EngineInstaller : IInstaller
    {
        readonly bool _isServer;

        public EngineInstaller(bool isServer) { _isServer = isServer; }

        public void Install(IContainerBuilder builder)
        {
            builder.Register<NullTransport>(Lifetime.Singleton).As<ITransport>();
            builder.Register<Network>(c => new Network(c.Resolve<ITransport>()), Lifetime.Singleton);

            builder.Register<EventStream>(Lifetime.Singleton)
                .AsSelf()
                .As<IEventEmitter>();

            builder.Register<HandlerRegistry>(Lifetime.Singleton);

            if (_isServer)
            {
                builder.Register<GameEngine>(Lifetime.Singleton);
                builder.Register<WireForwarder>(Lifetime.Singleton);
            }
            else
            {
                builder.Register<NetworkInboundBridge>(Lifetime.Singleton);
            }

            // Eager resolve: these classes wire via ctor side-effects, not a lifecycle interface.
            builder.RegisterBuildCallback(c => c.Resolve<HandlerRegistry>());

            // Populate the wire channel table before anything can emit. Both server and
            // client run the same registration so channel ids line up across the wire.
            builder.RegisterBuildCallback(c => WireRegistrar.RegisterAll(c.Resolve<Network>()));

            if (_isServer) builder.RegisterBuildCallback(c => c.Resolve<WireForwarder>());
            else           builder.RegisterBuildCallback(c => c.Resolve<NetworkInboundBridge>());
        }
    }
}
