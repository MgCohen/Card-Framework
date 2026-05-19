#nullable enable
using Scaffold.GraphFlow;

namespace Scaffold.GraphFlow.CardSandbox
{
    /// <summary>OnPlay imperative entry payload. Host invokes via controller.Run.</summary>
    public sealed class OnPlay : IGraphEntry
    {
        public object? Target;
    }
}
