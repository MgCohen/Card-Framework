namespace Trace.Handlers.Commands;

public sealed record TraceEvent(TraceKind Kind, long Id, long? Parent, int Depth, object Message, string Outcome);
