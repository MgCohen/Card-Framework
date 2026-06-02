using Framework.States;
using VContainer;

namespace Framework.Bootstrap
{
    public sealed class StateLayer : BootstrapLayer
    {
        public StateLayer(bool isServer) : base(isServer) { }

        public override void Install(IContainerBuilder builder)
        {
            builder.Register<StateStore>(Lifetime.Singleton);
            builder.RegisterInstance(GameStateRegistry.WithBaselines());
            if (_isServer) builder.Register<StateService>(Lifetime.Singleton);

            builder.RegisterHandler<StatePushHandler>();
        }
    }
}
