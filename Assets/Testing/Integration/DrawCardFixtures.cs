using Framework.Requests;
using Framework.Wire;
using Scaffold.AutoPacker;

namespace Framework.Testing.Integration
{
    // Request/response machinery for the integration spines. Kept here (not in Support) so the
    // Requests + Scaffold.AutoPacker references stay confined to the mechanism tier — the
    // Acceptance tier never sees them. The sample tags/commands live in Framework.Testing.Support.

    // A typed request/response pair. [Packed] partial mirrors every other WireEvent subclass;
    // this flow never serializes (single in-process stream), but the shape stays honest.
    public partial class DrawCardRequest : Request<DrawCardResponse>
    {
        [Packed] public int Count;
    }

    public partial class DrawCardResponse : Response
    {
        [Packed] public int CardDefinitionId;
        [Packed] public int Cost;
    }

    // Apply handlers for the request pair. The Requests feature ships the service + store but
    // not the wire handlers that populate the store / resolve the awaiter — those are still
    // caller-supplied today (the command pipeline's Response routing will absorb this). Mirrors
    // the shape proven in Framework.Requests.Tests.
    sealed class DrawCardRequestHandler : WireEventHandler<DrawCardRequest>
    {
        readonly RequestStore _store;
        public DrawCardRequestHandler(RequestStore store) { _store = store; }
        public override void Apply(DrawCardRequest ev) => _store.Insert(ev);
    }

    sealed class DrawCardResponseHandler : WireEventHandler<DrawCardResponse>
    {
        readonly RequestStore _store;
        readonly RequestService _service;

        public DrawCardResponseHandler(RequestStore store, RequestService service)
        {
            _store = store;
            _service = service;
        }

        public override void Apply(DrawCardResponse ev)
        {
            _store.Remove(ev.RequestId);
            _service.Complete(ev);
        }
    }
}
