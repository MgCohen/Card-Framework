#nullable enable
namespace StateMachine.Engine
{
    /// <summary>
    /// What a command handler gets. Read world, enqueue follow-up commands
    /// (the OnEnter/OnExit "generator" outputs land here), emit wire events.
    /// </summary>
    public interface ICommandScope
    {
        World World { get; }

        /// <summary>Enqueue a follow-up command at the tail of the pipeline.</summary>
        void Enqueue(GameAction command);

        /// <summary>Append a wire event to the log.</summary>
        void Emit(WireEvent ev);
    }

    /// <summary>
    /// What a GameState's virtual methods see. Deliberately narrower than
    /// ICommandScope — predicates/generators don't get to Emit or Enqueue
    /// directly; they return commands and the handler enqueues them.
    /// </summary>
    public interface IStateScope
    {
        World World { get; }
        GameState Current { get; }
    }
}
