using Framework.RecordIds;

namespace Framework.Blocks.Tests
{
    public sealed class TestMessage : ISourced
    {
        public TestMessage(IEntity source) => Source = source;

        public IEntity Source { get; }
    }
}
