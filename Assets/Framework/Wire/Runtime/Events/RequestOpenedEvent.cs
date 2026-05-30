using Scaffold.AutoPacker;

namespace Framework.Wire
{
    public partial class RequestOpenedEvent : WireEvent<RequestOpenedEvent.Packed>
    {
        [Packed] public int  Id;
        [Packed] public int  ParentId;
        [Packed] public int  SourceId;
        [Packed] public int  ResponderId;
        [Packed] public byte OptionCount;
        [Packed] public bool IsCancellable;
    }
}
