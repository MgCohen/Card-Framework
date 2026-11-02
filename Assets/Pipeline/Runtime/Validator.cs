using System;
using Framework.Routing;

namespace Framework.Pipeline
{
    public abstract class Validator<TMessage> : IRouted
    {
        public abstract Type RouteType { get; }

        protected internal abstract bool Validate(TMessage message, out string reason);
    }
}
