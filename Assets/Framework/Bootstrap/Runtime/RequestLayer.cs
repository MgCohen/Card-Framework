using Framework.Requests;
using Scaffold.AppFlow;
using VContainer;

namespace Framework.Bootstrap
{
    public sealed class RequestLayer : IScopeLayer
    {
        readonly bool _isServer;

        public RequestLayer(bool isServer) { _isServer = isServer; }

        public void Install(IContainerBuilder builder)
        {
            builder.Register<RequestStore>(Lifetime.Singleton);
            if (_isServer) builder.Register<RequestService>(Lifetime.Singleton);
        }
    }
}
