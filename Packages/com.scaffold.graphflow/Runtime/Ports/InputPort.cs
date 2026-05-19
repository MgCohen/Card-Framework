#nullable enable
using System;
using System.Runtime.CompilerServices;
using Scaffold.Variables;

namespace Scaffold.GraphFlow
{
    public sealed class InputPort<T> : Port
    {
        OutputPort<T>? _source;

        public bool IsConnected => _source != null;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T Read(Flow flow) => _source is null ? default! : _source.Read(flow);

        internal void Connect(OutputPort<T> source) => _source = source;

        internal override void ConnectFrom(Port output)
        {
            if (output is not OutputPort<T> typed)
                throw new InvalidOperationException(
                    $"Bake: output port {output.GetType()} does not match input port InputPort<{typeof(T)}>.");
            _source = typed;
        }

        internal override void ConnectFromVariable(IVariableHandle handle)
        {
            if (handle is not IReadOnlyVariableHandle<T> typed)
                throw new InvalidOperationException(
                    $"Bake: variable handle {handle.GetType()} does not match input port InputPort<{typeof(T)}>.");
            _source = new OutputPort<T>(_ => typed.Value, cache: false);
        }
    }
}
