using System.Collections.Generic;
using System.Threading.Tasks;
using Framework.Wire;

namespace Framework.Requests
{
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

        internal void Complete<TResponse>(TResponse response) where TResponse : Response
        {
            if (!_pending.Remove(response.RequestId, out var pending)) return;
            ((PendingRequest<TResponse>)pending).Tcs.TrySetResult(response);
        }

        public void CancelAllPending()
        {
            foreach (var p in _pending.Values) p.Cancel();
            _pending.Clear();
        }
    }
}
