#nullable enable
namespace StateMachine.Engine
{
    /// <summary>
    /// Successor map. Engine asks the flow what's next when the current
    /// state's IsCompleted returns true. Returning null means "stay where
    /// you are" — typically only the terminal state does this.
    /// </summary>
    public interface IStateFlow
    {
        GameState? NextAfter(GameState current, IStateScope scope);
    }
}
