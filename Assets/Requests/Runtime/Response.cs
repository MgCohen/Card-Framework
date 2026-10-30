using Framework.Wire;
using Scaffold.AutoPacker;

namespace Framework.Requests
{
    public abstract partial class Response : WireEvent
    {
        [Packed] public int RequestId;
    }
}
