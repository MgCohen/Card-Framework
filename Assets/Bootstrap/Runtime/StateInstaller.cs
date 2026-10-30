using Framework.States;
using VContainer;

namespace Framework.Bootstrap
{
    public sealed class StateInstaller : IInstaller
    {
        readonly bool _isServer;

        public StateInstaller(bool isServer) { _isServer = isServer; }

        public void Install(IContainerBuilder builder)
        {
            builder.Register<StateStore>(Lifetime.Singleton);
            builder.RegisterInstance(GameStateRegistry.WithBaselines());
            if (_isServer) builder.Register<StateService>(Lifetime.Singleton);

            builder.RegisterHandler<StatePushHandler>();
        }
    }
}
