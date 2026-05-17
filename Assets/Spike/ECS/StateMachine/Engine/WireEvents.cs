#nullable enable
namespace StateMachine.Engine
{
    /// <summary>
    /// Base for pure-data wire records. Same pattern as the TypedPayloads
    /// spike: no Apply/Revert/visitor — consumers each switch on the
    /// concrete type independently.
    /// </summary>
    public abstract class WireEvent { }

    /// <summary>
    /// The 2-byte payload from spike §v6. Apply: service.Current =
    /// registry.Resolve(NewKind). Unapply: service.Current =
    /// registry.Resolve(PrevKind). Self-contained do/undo — no polymorphism
    /// on the wire because the state record itself carries zero data.
    /// </summary>
    public sealed class StatePushEvent : WireEvent
    {
        public byte NewKind  { get; }
        public byte PrevKind { get; }

        public StatePushEvent(byte newKind, byte prevKind)
        { NewKind = newKind; PrevKind = prevKind; }

        public override string ToString() => $"StatePush(prev={PrevKind}, new={NewKind})";
    }

    /// <summary>
    /// Minimal command-fired marker — every handler emits one so tests can
    /// inspect the event stream and see OnEnter/OnExit-generated commands
    /// landing as real events. In a full engine each command would produce
    /// its own typed wire record (TagSet, TagChange, Block-open/close, etc.).
    /// </summary>
    public sealed class CommandExecutedEvent : WireEvent
    {
        public string CommandName { get; }
        public CommandExecutedEvent(string commandName) { CommandName = commandName; }
        public override string ToString() => $"CommandExecuted({CommandName})";
    }
}
