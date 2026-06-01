namespace Framework.States
{
    // Read-only view over StateStore (the FSM cursor Cell). GameEngine exposes this, not the
    // concrete store, so consumers read Current but never write — writes go through
    // StateService → handler.
    public interface IReadOnlyStateStore
    {
        GameState Current { get; }
    }
}
