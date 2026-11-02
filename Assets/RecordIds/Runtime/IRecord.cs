namespace Framework.RecordIds
{
    public interface IRecord<TId> where TId : Id
    {
        TId Id { get; }
    }
}
