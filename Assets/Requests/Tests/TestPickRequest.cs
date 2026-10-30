using Scaffold.AutoPacker;

namespace Framework.Requests.Tests
{
    // Minimal concrete Request — adds a scalar OptionCount so we can verify the leaf-only
    // fields round-trip alongside the inherited Id/ParentId/SourceId/ResponderId/IsCancellable.
    public partial class TestPickRequest : Request<TestPickResponse>
    {
        [Packed] public int OptionCount;
    }
}
