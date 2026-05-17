#nullable enable
namespace StateMachine.Engine
{
    /// <summary>
    /// Owns the most-recently-pushed GameState. Sibling to IRequestService in
    /// the full framework. Push() is sugar — it constructs a PushStateCommand
    /// and hands it to the engine, so transitions always flow through the
    /// pipeline (uniform with every other command).
    /// </summary>
    public interface IStateService
    {
        GameState Current { get; }
        void Push(GameState next);
    }

    public sealed class StateService : IStateService
    {
        private readonly GameEngine _engine;
        public GameState Current { get; internal set; }

        public StateService(GameEngine engine, GameState initial)
        {
            _engine = engine;
            Current = initial;
        }

        public void Push(GameState next) => _engine.Enqueue(new PushStateCommand(next));
    }
}
