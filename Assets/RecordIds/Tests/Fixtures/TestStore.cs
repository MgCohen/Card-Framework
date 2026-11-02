namespace Framework.RecordIds.Tests
{
    public sealed class TestStore : RecordStore<TestRecord, AId>
    {
        public void Add(TestRecord record) => Write(record);

        public bool Remove(AId id) => Erase(id);
    }
}
