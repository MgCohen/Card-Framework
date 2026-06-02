using System;
using System.Collections.Generic;

namespace Framework.Requests
{
    public sealed class RequestStore : IReadOnlyRequestStore
    {
        readonly Dictionary<int, Request> _open = new();

        public IReadOnlyDictionary<int, Request> Open => _open;

        // Absent id is a normal query miss (null); a present-but-wrong-type lookup is a bug (throw).
        public T Get<T>(int id) where T : Request
        {
            if (!_open.TryGetValue(id, out var req)) return null;
            if (req is T typed) return typed;
            throw new InvalidOperationException($"Request {id} is {req.GetType().Name}, not {typeof(T).Name}.");
        }

        // Sole writer: apply-handlers only.
        internal void Insert(Request req) => _open[req.Id] = req;
        internal void Remove(int id)      => _open.Remove(id);
    }
}
