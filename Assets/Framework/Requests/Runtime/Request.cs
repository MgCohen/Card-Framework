using Framework.Wire;
using Scaffold.AutoPacker;

namespace Framework.Requests
{
    public abstract partial class Request : WireEvent
    {
        [Packed] public int  Id;
        [Packed] public int  ParentId;
        [Packed] public int  SourceId;
        [Packed] public int  ResponderId;
        [Packed] public bool IsCancellable;
    }
}
