using Framework.Wire;
using Scaffold.AutoPacker;

namespace Framework.Requests
{
    // Non-generic Request base. Holds the shared metadata as plain [Packed] fields so the
    // RequestStore can key polymorphically (Dictionary<int, Request>) and so per-type
    // handlers don't have to redeclare these on every leaf. Concrete request classes
    // typically derive from Request<TResponse> (which adds TResponse for typed inference
    // at RequestService.Open), not from Request directly.
    public abstract partial class Request : WireEvent
    {
        [Packed] public int  Id;
        [Packed] public int  ParentId;
        [Packed] public int  SourceId;
        [Packed] public int  ResponderId;
        [Packed] public bool IsCancellable;
    }
}
