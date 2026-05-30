using System.Collections.Generic;
using System.Threading.Tasks;
using Framework.Wire;

namespace Framework.Requests
{
    // Command surface over RequestStore. Server-only — clients read the store directly
    // and submit responses through the wire (today via test loopback; the production
    // client → server delivery is a wire-architecture concern flagged in the spike doc).
    //
    // Open allocates the id, parks a TCS, and emits the Request wire event. The per-type
    // apply handler picks up the emit, inserts into RequestStore. Same handler runs on
    // the client when the wire delivers — same Insert. When the matching Response wire
    // event arrives back, the per-type response handler removes from the store and calls
    // Complete<TResponse> here, which resolves the TCS for the awaiter.
    public sealed class RequestService
    {
        readonly IEventEmitter _events;
        readonly Dictionary<int, IPendingRequest> _pending = new();
        int _nextId = 1;

        public RequestService(IEventEmitter events)
        {
            _events = events;
        }

        public Task<TResponse> Open<TResponse>(Request<TResponse> request)
            where TResponse : Response
        {
            request.Id = _nextId++;
            var pending = new PendingRequest<TResponse>();
            _pending[request.Id] = pending;
            _events.Emit(request);
            return pending.Tcs.Task;
        }

        // Called by per-response-type apply handlers on the server.
        internal void Complete<TResponse>(TResponse response) where TResponse : Response
        {
            if (!_pending.Remove(response.RequestId, out var pending)) return;
            ((PendingRequest<TResponse>)pending).Tcs.TrySetResult(response);
        }

        // Match-end hook (called when MatchService lands).
        public void CancelAllPending()
        {
            foreach (var p in _pending.Values) p.Cancel();
            _pending.Clear();
        }
    }
}
