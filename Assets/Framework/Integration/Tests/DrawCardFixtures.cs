using Framework.Requests;
using Framework.Tags;
using Framework.Wire;
using Scaffold.AutoPacker;

namespace Framework.Integration.Tests
{
    // Game-side tag keys for the flow (records over Tag<T>, exposed as static aliases —
    // the standard ergonomic). Slots are arbitrary; Set/Get index the store by slot directly.
    public sealed record HealthTag() : Tag<int>(2001);
    public sealed record CostTag()   : Tag<int>(2002);
    public sealed record IsCardTag() : Tag<bool>(2003);

    static class SampleTags
    {
        public static readonly Tag<int>  Health = new HealthTag();
        public static readonly Tag<int>  Cost   = new CostTag();
        public static readonly Tag<bool> IsCard = new IsCardTag();
    }

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
