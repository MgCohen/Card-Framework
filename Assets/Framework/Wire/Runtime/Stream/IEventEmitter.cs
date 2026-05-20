namespace Framework.Wire
{
    public interface IEventEmitter
    {
        void Emit<TEvent>(TEvent ev) where TEvent : WireEvent;
        void Emit(WireEvent ev);
    }
}
