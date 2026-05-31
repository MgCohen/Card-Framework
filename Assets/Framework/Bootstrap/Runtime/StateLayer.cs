using Framework.States;
using Scaffold.AppFlow;
using VContainer;

namespace Framework.Bootstrap
{
    // StateStore + the per-state apply handlers register on both sides — both mirror the FSM
    // cursor. StateService — the Push surface — is server-only: clients read the store and
    // never author transitions. RegisterStateType<TState> wires one state's handler; one call
    // per state. The framework ships the three baselines; games register their own kinds in
    // their own layer.
    public sealed class StateLayer : IScopeLayer
    {
        readonly bool _isServer;

        public StateLayer(bool isServer) { _isServer = isServer; }

        public void Install(IContainerBuilder builder)
        {
            builder.Register<StateStore>(Lifetime.Singleton);
            if (_isServer) builder.Register<StateService>(Lifetime.Singleton);

            builder.RegisterStateType<MatchNotStartedState>();
            builder.RegisterStateType<PlayingState>();
            builder.RegisterStateType<MatchEndedState>();
        }
    }
}
