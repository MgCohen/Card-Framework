using Scaffold.AutoPacker;

namespace Framework.Wire
{
    public partial class ResponseEvent : WireEvent<ResponseEvent.Packed>
    {
        [Packed] public int  RequestId;
        [Packed] public byte Picked;
    }
}
