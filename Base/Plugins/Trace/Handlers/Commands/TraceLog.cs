using VSA.Contracts;

namespace Trace.Handlers.Commands;

public sealed class TraceLog
{
    public IReadOnlyList<TraceEvent> Events => throw new NotImplementedException();

    public bool Recording => throw new NotImplementedException();

    public void Start()
    {
        throw new NotImplementedException();
    }

    public void Add(TraceEvent entry)
    {
        throw new NotImplementedException();
    }

    public void Wire(int entry, IMessage message)
    {
        throw new NotImplementedException();
    }

    public void Clear()
    {
        throw new NotImplementedException();
    }
}
