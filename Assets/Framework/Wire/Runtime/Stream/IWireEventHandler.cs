using System;

namespace Framework.Wire
{
    public interface IWireEventHandler
    {
        Type EventType { get; }
        void Apply(WireEvent ev);
        void Unapply(WireEvent ev);
    }
}
