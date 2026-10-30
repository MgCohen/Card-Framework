using Framework.Core;

namespace Framework.States
{
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
