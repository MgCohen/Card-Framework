using System.Threading.Tasks;

namespace Framework.Requests
{
    // Server-only live state for an open request. Holds the TaskCompletionSource that
    // the awaiter is parked on. RequestService keeps these in a Dictionary<int, IPendingRequest>
    // (non-generic — TResponse varies per request type); IPendingRequest gives a uniform
    // Cancel handle for match-end cleanup.
    internal interface IPendingRequest
    {
        void Cancel();
    }

    internal sealed class PendingRequest<TResponse> : IPendingRequest where TResponse : Response
    {
        public TaskCompletionSource<TResponse> Tcs { get; } = new();
        public void Cancel() => Tcs.TrySetCanceled();
    }
}
