using System;
using Framework.TypeRouting;

namespace Framework.Pipeline
{
    public abstract class Validator<TMessage> : IRouted where TMessage : Message
    {
        public abstract Type RouteType { get; }

        protected internal abstract bool Validate(TMessage message, out string reason);
    }
}
