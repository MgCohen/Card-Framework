using System;

namespace Framework.Wire
{
    public abstract class WireEventHandler<T> : IWireEventHandler where T : WireEvent
    {
        public Type EventType => typeof(T);

        public abstract void Apply(T ev);

        public virtual void Unapply(T ev) => throw new NotImplementedException("Rollback lights up later");

        void IWireEventHandler.Apply(WireEvent ev)   => Apply((T)ev);
        void IWireEventHandler.Unapply(WireEvent ev) => Unapply((T)ev);
    }
}
