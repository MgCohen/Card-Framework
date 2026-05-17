#nullable enable
namespace StateMachine.Engine
{
    /// <summary>
    /// Base for everything that flows through the pipeline. Records so game
    /// authors get value-equality + concise syntax. Result is the matching
    /// shape returned by the handler.
    /// </summary>
    public abstract record GameAction;

    public abstract record CommandResult;

    public sealed record VoidResult : CommandResult
    {
        public static readonly VoidResult Instance = new();
    }
}
