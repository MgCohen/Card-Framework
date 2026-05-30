using Scaffold.AutoPacker;

namespace Framework.Wire
{
    public partial class ResponseEvent : WireEvent
    {
        [Packed] public int  RequestId;
        [Packed] public byte Picked;
    }
}
