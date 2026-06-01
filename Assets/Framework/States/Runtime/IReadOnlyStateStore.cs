namespace Framework.States
{
    public interface IReadOnlyStateStore
    {
        GameState Current { get; }
    }
}
