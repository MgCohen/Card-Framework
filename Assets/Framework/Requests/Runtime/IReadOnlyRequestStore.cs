using System.Collections.Generic;

namespace Framework.Requests
{
    // Read-only view over RequestStore. GameEngine exposes this, not the concrete store, so
    // consumers query open requests but never write — writes go through RequestService → handler.
    public interface IReadOnlyRequestStore
    {
        IReadOnlyDictionary<int, Request> Open { get; }
        T Get<T>(int id) where T : Request;
    }
}
