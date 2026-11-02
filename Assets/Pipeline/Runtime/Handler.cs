using System;
using System.Threading.Tasks;
using Framework.TypeRouting;

namespace Framework.Pipeline
{
    // The router member. Derive from Handler<TBase, TMessage, TScope>: the internal Dispatch blocks other subclasses.
    public abstract class Handler<TBase, TScope> : IRouted where TBase : Message
    {
        public abstract Type RouteType { get; }

        internal abstract Task<object> Dispatch(TBase message, TScope scope);
    }

    public abstract class Handler<TBase, TMessage, TScope> : Handler<TBase, TScope>
        where TBase : Message
        where TMessage : TBase
    {
        public sealed override Type RouteType => typeof(TMessage);

        // The router gives this handler only messages of type TMessage, so the cast cannot fail.
        internal sealed override Task<object> Dispatch(TBase message, TScope scope) => Handle((TMessage)message, scope);

        protected internal abstract Task<object> Handle(TMessage message, TScope scope);
    }
}
