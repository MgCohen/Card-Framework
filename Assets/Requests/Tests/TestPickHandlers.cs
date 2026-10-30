using Framework.Wire;

namespace Framework.Requests.Tests
{
    // Per-type apply handlers for the test fixture. Same shape both sides — Insert/Remove
    // run on server and client. On the server, the response handler additionally calls
    // service.Complete to resolve the awaiter's TCS; on the client, RequestService is null
    // (not registered) and the call is skipped.
    sealed class TestPickRequestHandler : WireEventHandler<TestPickRequest>
    {
        readonly RequestStore _store;
        public TestPickRequestHandler(RequestStore store) { _store = store; }
        public override void Apply(TestPickRequest ev) => _store.Insert(ev);
    }

    sealed class TestPickResponseHandler : WireEventHandler<TestPickResponse>
    {
        readonly RequestStore _store;
        readonly RequestService _service;  // server-only; null on client

        public TestPickResponseHandler(RequestStore store, RequestService service = null)
        {
            _store = store;
            _service = service;
        }

        public override void Apply(TestPickResponse ev)
        {
            _store.Remove(ev.RequestId);
            _service?.Complete(ev);
        }
    }
}
