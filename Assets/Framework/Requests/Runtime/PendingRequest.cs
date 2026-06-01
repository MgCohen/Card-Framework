using System.Threading.Tasks;

namespace Framework.Requests
{
    internal sealed class PendingRequest<TResponse> : IPendingRequest where TResponse : Response
    {
        public TaskCompletionSource<TResponse> Tcs { get; } = new();
        public void Cancel() => Tcs.TrySetCanceled();
    }
}
