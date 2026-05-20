namespace Framework.Wire
{
    public interface IHandlerRegistry
    {
        void Register<TEvent>(IWireEventHandler<TEvent> handler) where TEvent : WireEvent;
    }
}
