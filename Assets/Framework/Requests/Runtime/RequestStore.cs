using System.Collections.Generic;

namespace Framework.Requests
{
    // Holds the currently-open requests, keyed by Request.Id. Same shape both sides:
    // server inserts when its service.Open fires; client inserts when the request wire
    // event arrives. Both remove when the Response wire event arrives. Reconstructable
    // entirely from the wire — no server-only state lives here.
    //
    // Server-only TCS state lives on RequestService in a separate dictionary.
    public sealed class RequestStore
    {
        readonly Dictionary<int, Request> _open = new();

        public IReadOnlyDictionary<int, Request> Open => _open;

        public T Get<T>(int id) where T : Request
            => _open.TryGetValue(id, out var req) ? req as T : null;

        internal void Insert(Request req) => _open[req.Id] = req;
        internal void Remove(int id)      => _open.Remove(id);
    }
}
