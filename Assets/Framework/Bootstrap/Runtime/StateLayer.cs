using Framework.States;
using Scaffold.AppFlow;
using VContainer;

namespace Framework.Bootstrap
{
    // StateStore + the single StatePushHandler register on both sides — both mirror the FSM
    // cursor. StateService — the Push surface — is server-only: clients read the store and
    // never author transitions. State is now slot-discriminated: one handler resolves every
    // transition via the GameStateRegistry, instead of one handler per state. The framework
    // ships the three baselines via WithBaselines(); games register their own kinds on top.
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
