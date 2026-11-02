namespace Framework.RecordIds.Tests
{
    public sealed class TestRecord : IRecord<AId>
    {
        public TestRecord(AId id) => Id = id;

        public AId Id { get; }
    }
}
