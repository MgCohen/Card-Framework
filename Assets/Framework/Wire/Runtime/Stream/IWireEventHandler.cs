namespace Framework.Wire
{
    public interface IWireEventHandler<TEvent> where TEvent : WireEvent
    {
        void Apply(TEvent ev);
        void Unapply(TEvent ev);
    }
}
