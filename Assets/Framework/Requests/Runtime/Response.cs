using Framework.Wire;
using Scaffold.AutoPacker;

namespace Framework.Requests
{
    // Response base. Single non-generic — Response isn't parameterized on anything
    // (it carries only the correlation id back to its Request); concrete responses
    // just inherit Response and add their own [Packed] payload fields.
    public abstract partial class Response : WireEvent
    {
        [Packed] public int RequestId;
    }
}
