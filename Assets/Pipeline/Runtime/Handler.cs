using System;
using System.Threading.Tasks;
using Framework.TypeRouting;

namespace Framework.Pipeline
{
    public abstract class Handler<TMessage, TScope> : IRouted where TMessage : Message
    {
        public abstract Type RouteType { get; }

        protected internal abstract Task<object> Handle(TMessage message, TScope scope);
    }
}
