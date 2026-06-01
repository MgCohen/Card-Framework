using Framework.States;
using Scaffold.AppFlow;
using VContainer;

namespace Framework.Bootstrap
{
    public sealed class StateLayer : IScopeLayer
    {
        readonly bool _isServer;

        public StateLayer(bool isServer) { _isServer = isServer; }

        public void Install(IContainerBuilder builder)
        {
            builder.Register<StateStore>(Lifetime.Singleton);
            builder.RegisterInstance(GameStateRegistry.WithBaselines());
            if (_isServer) builder.Register<StateService>(Lifetime.Singleton);

            builder.RegisterHandler<StatePushHandler>();
        }
    }
}
