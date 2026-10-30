namespace Framework.Tags
{
    public interface IReadOnlyTagStore
    {
        T Get<T>(int entityId, int slot) where T : unmanaged;
        bool Has(int entityId, int slot);
    }
}
