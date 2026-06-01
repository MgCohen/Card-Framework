using Framework.Core;

namespace Framework.States
{
    // Registry of FSM kinds — a scoped instance (never a static global). A thin
    // SlotRegistry<GameState>; all behaviour is inherited (Register<T> / Register(member) /
    // BySlot / Of<T>). WithBaselines() loads the framework states; each composition root calls
    // it explicitly (StateLayer per scope, the test harness in tests). Games register their own
    // kinds on top.
    public sealed class GameStateRegistry : SlotRegistry<GameState>
    {
        public static GameStateRegistry WithBaselines()
        {
            var registry = new GameStateRegistry();
            registry.Register<MatchNotStartedState>();
            registry.Register<PlayingState>();
            registry.Register<MatchEndedState>();
            return registry;
        }
    }
}
