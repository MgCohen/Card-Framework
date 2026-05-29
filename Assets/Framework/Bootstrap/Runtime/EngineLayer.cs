using Framework.Engine;
using Framework.Wire;
using Scaffold.AppFlow;
using VContainer;

namespace Framework.Bootstrap
{
    // Engine substrate + the side-asymmetric wire bridge. The build callback forces resolve
    // on HandlerRegistry and the bridge — both classes do their work in their ctors
    // (subscription wiring). VContainer's RegisterEntryPoint alone wouldn't trigger them
    // because they don't implement any VContainer lifecycle interface.
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
                .As<IEventEmitter>();

            builder.Register<HandlerRegistry>(Lifetime.Singleton);
            builder.Register<GameEngine>(Lifetime.Singleton);

            if (_isServer) builder.Register<WireForwarder>(Lifetime.Singleton);
            else           builder.Register<NetworkInboundBridge>(Lifetime.Singleton);

            // Force eager resolve at container build — these classes wire via ctor side-effects.
            builder.RegisterBuildCallback(c => c.Resolve<HandlerRegistry>());
            if (_isServer) builder.RegisterBuildCallback(c => c.Resolve<WireForwarder>());
            else           builder.RegisterBuildCallback(c => c.Resolve<NetworkInboundBridge>());
        }
    }
}
