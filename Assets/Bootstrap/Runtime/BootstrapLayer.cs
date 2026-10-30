using Scaffold.AppFlow;
using VContainer;

namespace Framework.Bootstrap
{
    public abstract class BootstrapLayer : IScopeLayer
    {
        protected readonly bool _isServer;

        protected BootstrapLayer(bool isServer) { _isServer = isServer; }

        public abstract void Install(IContainerBuilder builder);
    }
}
