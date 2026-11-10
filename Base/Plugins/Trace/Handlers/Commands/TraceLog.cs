using VSA.Contracts;

namespace Trace.Handlers.Commands;

public sealed class TraceLog
{
    private readonly List<TraceEvent> events = new List<TraceEvent>();

    public IReadOnlyList<TraceEvent> Events => events;

    public bool Recording { get; private set; }

    public void Start()
    {
        Recording = true;
    }

    public void Add(TraceEvent entry)
    {
        if (Recording)
        {
            events.Add(entry);
        }
    }

    public void Wire(int entry, IMessage message)
    {
        TraceEvent? result = events.LastOrDefault(
            e => e.Kind == TraceKind.Result && ReferenceEquals(e.Message, message));
        string outcome = $"entry {entry}";
        Add(result is null
            ? new TraceEvent(TraceKind.Wire, 0, null, 0, message, outcome)
            : new TraceEvent(TraceKind.Wire, result.Id, result.Parent, result.Depth, message, outcome));
    }

    public void Clear()
    {
        events.Clear();
    }
}
