namespace Trace.Handlers.Commands;

public static class TraceText
{
    public static IReadOnlyList<string> Legend => throw new NotImplementedException();

    public static IEnumerable<string> Lines(IEnumerable<TraceEvent> events)
    {
        throw new NotImplementedException();
    }

    public static string Name(Type type)
    {
        throw new NotImplementedException();
    }

    public static string Payload(object message)
    {
        throw new NotImplementedException();
    }
}
