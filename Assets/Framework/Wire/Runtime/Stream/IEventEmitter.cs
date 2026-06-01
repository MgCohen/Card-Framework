namespace Framework.Wire
{
    public interface IEventEmitter
    {
        void Emit(WireEvent ev);
    }
}
