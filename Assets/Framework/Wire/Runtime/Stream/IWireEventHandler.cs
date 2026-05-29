using System;

namespace Framework.Wire
{
    // Non-generic marker so HandlerRegistry can collect every closed handler via DI list
    // injection. Concrete handlers don't implement this directly — they derive from
    // WireEventHandler<T>, which supplies the boxed Apply/Unapply shims and the EventType.
    public interface IWireEventHandler
    {
        Type EventType { get; }
        void Apply(WireEvent ev);
        void Unapply(WireEvent ev);
    }
}
