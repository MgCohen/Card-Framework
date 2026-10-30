using System.Collections.Generic;

namespace Framework.Requests
{
    public interface IReadOnlyRequestStore
    {
        IReadOnlyDictionary<int, Request> Open { get; }
        T Get<T>(int id) where T : Request;
    }
}
