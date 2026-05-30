using Scaffold.AutoPacker;

namespace Framework.Requests.Tests
{
    public partial class TestPickResponse : Response
    {
        [Packed] public int Picked;
    }
}
