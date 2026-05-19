#nullable enable
using System;
using System.Runtime.CompilerServices;
using Cysharp.Threading.Tasks;

namespace Scaffold.GraphFlow
{
    public sealed class FlowInPort : Port
    {
        public RuntimeNode Owner { get; }
        public string Name { get; }
        public FlowConnection? Connection { get; internal set; }

        // Exactly one of these is set; Sync() / Async() factories enforce that.
        // Branching once on the field is cheaper than wrapping the user's sync
        // handler in a flow=>UniTask(handler(flow)) closure on every fire.
        //
        // Async path uses Cysharp.Threading.Tasks.UniTask: struct-based promise +
        // pooled async state machine builder → ~0 GC per yielding node fire on
        // both Mono and IL2CPP. Replaces the Func<Flow, Task<>> shape that cost
        // 2 allocs per fire (state machine box + Task<FlowOutPort?>).
        readonly Func<Flow, FlowOutPort?>? _sync;
        readonly Func<Flow, UniTask<FlowOutPort?>>? _async;

        FlowInPort(RuntimeNode owner, string name,
                   Func<Flow, FlowOutPort?>? sync,
                   Func<Flow, UniTask<FlowOutPort?>>? @async)
        {
            Owner = owner;
            Name = name;
            _sync = sync;
            _async = @async;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal UniTask<FlowOutPort?> Invoke(Flow flow) =>
            _sync != null
                ? new UniTask<FlowOutPort?>(_sync(flow))
                : _async!(flow);

        public static FlowInPort Sync(
            RuntimeNode owner, string name, Func<Flow, FlowOutPort?> handler) =>
            new(owner, name, handler, null);

        public static FlowInPort Async(
            RuntimeNode owner, string name, Func<Flow, UniTask<FlowOutPort?>> handler) =>
            new(owner, name, null, handler);
    }
}
