using Scaffold.AutoPacker;

namespace Framework.Wire
{
    public partial class RequestClosedEvent : WireEvent
    {
        [Packed] public int Id;
    }
}
