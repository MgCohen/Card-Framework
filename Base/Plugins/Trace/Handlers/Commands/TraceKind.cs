namespace Trace.Handlers.Commands;

public enum TraceKind
{
    Send = 0,
    Reject = 1,
    Result = 2,
    Fault = 3,
    Wire = 4,
}
