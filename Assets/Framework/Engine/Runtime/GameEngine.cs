using Framework.Wire;

namespace Framework.Engine
{
    public sealed class GameEngine
    {
        public EventStream Stream { get; }
        public Network Network { get; }

        public GameEngine(EventStream stream, Network network)
        {
            Stream = stream;
            Network = network;
        }
    }
}
