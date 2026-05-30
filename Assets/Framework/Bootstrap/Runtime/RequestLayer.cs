using Framework.Requests;
using Scaffold.AppFlow;
using VContainer;

namespace Framework.Bootstrap
{
    // RequestStore registers on both sides — both need to mirror the open-requests view.
    // RequestService — the Open / Cancel surface — is server-only; clients don't allocate
    // request ids or park awaiters.
    //
    // No handlers register here: the framework ships only the Request / Response bases.
    // Concrete Request / Response types and their per-type apply handlers live in game
    // code (or in test fixtures); games call builder.RegisterHandler<MyRequestHandler>()
    // for each in their own layer.
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
