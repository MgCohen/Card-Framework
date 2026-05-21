using Framework.Engine;
using Framework.Wire;
using Scaffold.AppFlow;
using VContainer;

namespace Framework.Bootstrap
{
    public sealed class EngineLayer : IScopeLayer
    {
        public void Install(IContainerBuilder builder)
        {
            builder.Register<NullTransport>(Lifetime.Singleton).As<ITransport>();
            builder.Register<Network>(c => new Network(c.Resolve<ITransport>()), Lifetime.Singleton);

            builder.Register<EventStream>(Lifetime.Singleton)
                .AsSelf()
                .As<IEventEmitter>()
                .As<IHandlerRegistry>();

            builder.Register<GameEngine>(Lifetime.Singleton);
        }
    }
}
