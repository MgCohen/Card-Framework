#nullable enable
namespace StateMachine.Engine
{
    /// <summary>Spike §v6: replaces ADR-0009's TransitionStateCommand.</summary>
    public sealed record PushStateCommand(GameState NewState) : GameAction;

    public sealed record PushStateResult : CommandResult;

    /// <summary>
    /// The lifecycle from spike §v6:
    ///   1. old.OnExit(scope)  → enqueue returned commands
    ///   2. emit StatePushEvent on wire
    ///   3. swap service.Current = newState
    ///   4. newState.OnEnter(scope) → enqueue returned commands
    ///
    /// Enqueued OnExit/OnEnter outputs flow through the normal pipeline,
    /// produce their own wire events, get recorded in history. Replay walks
    /// events; hooks never re-fire (Q6 decision).
    /// </summary>
    public sealed class PushStateHandler : ICommandHandler<PushStateCommand, PushStateResult>
    {
        private readonly StateService   _state;
        private readonly IStateRegistry _registry;

        public PushStateHandler(StateService state, IStateRegistry registry)
        { _state = state; _registry = registry; }

        public PushStateResult Handle(PushStateCommand cmd, ICommandScope scope)
        {
            var prev = _state.Current;
            var next = cmd.NewState;

            var stateScope = new StateScope(scope.World, prev);

            foreach (var c in prev.OnExit(stateScope))
                scope.Enqueue(c);

            scope.Emit(new StatePushEvent(
                newKind:  _registry.KindOf(next),
                prevKind: _registry.KindOf(prev)));

            _state.Current = next;

            var enteredScope = new StateScope(scope.World, next);
            foreach (var c in next.OnEnter(enteredScope))
                scope.Enqueue(c);

            return new PushStateResult();
        }
    }
}
