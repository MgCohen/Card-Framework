#nullable enable
using System;
using Scaffold.Variables;

namespace Scaffold.GraphFlow
{
    public abstract class Port
    {
        internal virtual void ConnectFrom(Port output) =>
            throw new InvalidOperationException(
                $"ConnectFrom is only valid on InputPort<T>; got {GetType()}.");

        internal virtual void ConnectFromVariable(IVariableHandle handle) =>
            throw new InvalidOperationException(
                $"ConnectFromVariable is only valid on InputPort<T>; got {GetType()}.");

        // Called by GraphBuilder after the runner is constructed and before
        // node Initialize() runs. Cached output ports use this to size their
        // per-flow Entry[] to MaxConcurrentFlows. No-op for everything else.
        internal virtual void Bake(int maxFlows) { }
    }
}
