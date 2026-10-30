using Framework.Requests;
using VContainer;

namespace Framework.Bootstrap
{
    public sealed class RequestInstaller : IInstaller
    {
        readonly bool _isServer;

        public RequestInstaller(bool isServer) { _isServer = isServer; }

        public void Install(IContainerBuilder builder)
        {
            builder.Register<RequestStore>(Lifetime.Singleton);
            if (_isServer) builder.Register<RequestService>(Lifetime.Singleton);
        }
    }
}
