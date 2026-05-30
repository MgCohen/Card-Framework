using System;

namespace Framework.Requests
{
    // Generic wrapper — adds TResponse purely for type inference at
    // RequestService.Open(Request<TResponse>) → Task<TResponse>. Carries no [Packed]
    // fields of its own; the shared metadata lives on the non-generic Request base.
    public abstract partial class Request<TResponse> : Request where TResponse : Response
    {
        public Type ResponseType => typeof(TResponse);
    }
}
