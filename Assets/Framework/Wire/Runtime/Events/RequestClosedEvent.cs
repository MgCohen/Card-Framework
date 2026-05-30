using Scaffold.AutoPacker;

namespace Framework.Wire
{
    public partial class RequestClosedEvent : WireEvent<RequestClosedEvent.Packed>
    {
        [Packed] public int Id;
    }
}
