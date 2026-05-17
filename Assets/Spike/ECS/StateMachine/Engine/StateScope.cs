#nullable enable
namespace StateMachine.Engine
{
    internal sealed class StateScope : IStateScope
    {
        public World     World   { get; }
        public GameState Current { get; }

        public StateScope(World world, GameState current)
        { World = world; Current = current; }
    }
}
