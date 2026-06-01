using Framework.Engine;
using Framework.Wire;
using VContainer;

namespace Framework.Bootstrap
{
    public sealed class EngineLayer : BootstrapLayer
    {
        public EngineLayer(bool isServer) : base(isServer) { }

        public override void Install(IContainerBuilder builder)
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
            if (_isServer) builder.RegisterBuildCallback(c => c.Resolve<WireForwarder>());
            else           builder.RegisterBuildCallback(c => c.Resolve<NetworkInboundBridge>());
        }
    }
}
