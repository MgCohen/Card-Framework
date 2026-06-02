using Framework.Requests;
using VContainer;

namespace Framework.Bootstrap
{
    public sealed class RequestLayer : BootstrapLayer
    {
        public RequestLayer(bool isServer) : base(isServer) { }

        public override void Install(IContainerBuilder builder)
        {
            builder.Register<RequestStore>(Lifetime.Singleton);
            if (_isServer) builder.Register<RequestService>(Lifetime.Singleton);
        }
    }
}
