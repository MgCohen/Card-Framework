using System;
using System.Threading.Tasks;
using Framework.Routing;

namespace Framework.Pipeline
{
    public abstract class Handler<TMessage, TScope> : IRouted
    {
        public abstract Type RouteType { get; }

        protected internal abstract Task<object> Handle(TMessage message, TScope scope);
    }
}
