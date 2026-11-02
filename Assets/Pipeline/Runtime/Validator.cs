using System;
using Framework.TypeRouting;

namespace Framework.Pipeline
{
    // The router member. Derive from Validator<TBase, TMessage, TView>: the internal Dispatch blocks other subclasses.
    public abstract class Validator<TBase, TView> : IRouted where TBase : Message
    {
        public abstract Type RouteType { get; }

        internal abstract bool Dispatch(TBase message, TView view, out string reason);
    }

    public abstract class Validator<TBase, TMessage, TView> : Validator<TBase, TView>
        where TBase : Message
        where TMessage : TBase
    {
        public sealed override Type RouteType => typeof(TMessage);

        // The router gives this validator only messages of type TMessage, so the cast cannot fail.
        internal sealed override bool Dispatch(TBase message, TView view, out string reason)
            => Validate((TMessage)message, view, out reason);

        protected internal abstract bool Validate(TMessage message, TView view, out string reason);
    }
}
