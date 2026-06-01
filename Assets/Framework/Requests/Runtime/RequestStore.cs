using System.Collections.Generic;

namespace Framework.Requests
{
    public sealed class RequestStore : IReadOnlyRequestStore
    {
        readonly Dictionary<int, Request> _open = new();

        public IReadOnlyDictionary<int, Request> Open => _open;

        public T Get<T>(int id) where T : Request
            => _open.TryGetValue(id, out var req) ? req as T : null;

        // Sole writer: apply-handlers only.
        internal void Insert(Request req) => _open[req.Id] = req;
        internal void Remove(int id)      => _open.Remove(id);
    }
}
