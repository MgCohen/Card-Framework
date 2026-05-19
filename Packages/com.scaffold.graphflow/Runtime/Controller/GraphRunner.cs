#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Scaffold.Variables;

namespace Scaffold.GraphFlow
{
    public abstract class GraphRunner
    {
        public IReadOnlyList<RuntimeNode> Nodes { get; }
        public IReadOnlyDictionary<Type, EntryRuntimeNodeBase> EntriesByPayload { get; }
        public IVariableBag Variables { get; internal set; } = null!;

        // Cap on concurrent flows running against this runner. Each Flow
        // takes one index from this pool for its lifetime; Complete() returns
        // it. Every cached output port allocates an Entry[MaxConcurrentFlows],
        // so raising this costs memory linearly across cached ports.
        public int MaxConcurrentFlows { get; }
        readonly Stack<int> _freeFlowIndices;
        int _versionCounter;

        // Deferred release tracker: the most recently produced Flow (any
        // type). The NEXT call to Run() releases this back to its typed pool
        // before acquiring a fresh one. That gives callers the full window
        // between `await runner.Run(...)` returning and their next Run() call
        // to read flow.Outcome / flow.Result / flow.Variables, while still
        // letting the pool reuse the instance.
        Flow? _lastFlow;

        protected GraphRunner(BakedGraph baked, int maxConcurrentFlows = 8)
        {
            if (maxConcurrentFlows < 1)
                throw new ArgumentOutOfRangeException(
                    nameof(maxConcurrentFlows),
                    maxConcurrentFlows,
                    "MaxConcurrentFlows must be at least 1.");
            Nodes = baked.Nodes;
            EntriesByPayload = baked.EntriesByPayload;
            MaxConcurrentFlows = maxConcurrentFlows;
            _freeFlowIndices = new Stack<int>(maxConcurrentFlows);
            // Push in reverse so first Acquire returns 0; deterministic for tests.
            for (int i = maxConcurrentFlows - 1; i >= 0; i--)
                _freeFlowIndices.Push(i);
        }

        internal int AcquireFlowIndex()
        {
            if (_freeFlowIndices.Count == 0)
                throw new InvalidOperationException(
                    $"GraphRunner: MaxConcurrentFlows={MaxConcurrentFlows} exhausted. "
                    + "Raise the cap by passing maxConcurrentFlows to base(...).");
            return _freeFlowIndices.Pop();
        }

        internal void ReleaseFlowIndex(int index)
        {
            _freeFlowIndices.Push(index);
        }

        // Monotonic per-runner version source for OutputPort cache freshness.
        internal int NextCacheVersion() => ++_versionCounter;

        /// <summary>Override to fully construct the runner's variable bag —
        /// seed the graph-declared variables and chain to whatever consumer
        /// storage you want (shared variables, save state, network store,
        /// state-backed bag, ...). Per Invariant 2, the consumer owns bag
        /// construction so graph-declared variables can flow through any
        /// custom storage instead of always landing in an in-memory bag.
        /// Default materializes everything in-memory with no parent.</summary>
        protected internal virtual IVariableBag CreateVariableBag(IEnumerable<RuntimeVariable> seed)
            => CreateInMemoryBag(seed);

        /// <summary>Convenience for overrides that still want the standard
        /// in-memory materialization but need a different parent (e.g. a
        /// consumer-supplied global bag).</summary>
        protected static InMemoryVariableBag CreateInMemoryBag(
            IEnumerable<RuntimeVariable> seed, IVariableBag? parent = null)
        {
            var bag = new InMemoryVariableBag(parent);
            if (seed == null) return bag;
            foreach (var v in seed)
            {
                if (v == null || string.IsNullOrEmpty(v.id) || v.defaultValue == null) continue;
                bag.Add(v.defaultValue.CreateHandle(v.id));
            }
            return bag;
        }

        public virtual void Initialize() { }

        // Public surface returns UniTask<Flow<>> — zero allocation on the
        // sync path (struct-based promise) with no .AsTask() Task wrapper.
        // Callers `await runner.Run(...)` work identically; the only
        // observable change is `Task<Flow<>> t = runner.Run(...)` no longer
        // compiles — call `.AsTask()` explicitly if you need that.
        public UniTask<Flow<TEntry>> Run<TEntry>(TEntry payload, CancellationToken ct = default)
            where TEntry : class
        {
            var entry = ResolveEntry<TEntry>();
            ReleasePreviousFlow();
            var flow = Flow<TEntry>.Acquire(payload, this, ct);
            _lastFlow = flow;
            return RunFromEntry(entry, flow);
        }

        public UniTask<Flow<TEntry, TResult>> Run<TEntry, TResult>(TEntry payload, CancellationToken ct = default)
            where TEntry : class
        {
            var entry = ResolveEntry<TEntry>();
            ReleasePreviousFlow();
            var flow = Flow<TEntry, TResult>.Acquire(payload, this, ct);
            _lastFlow = flow;
            return RunFromEntry(entry, flow);
        }

        void ReleasePreviousFlow()
        {
            if (_lastFlow is null) return;
            _lastFlow.Release();   // virtual; dispatches to typed pool
            _lastFlow = null;
        }

        EntryRuntimeNodeBase ResolveEntry<TEntry>()
        {
            if (!EntriesByPayload.TryGetValue(typeof(TEntry), out var entry))
                throw new InvalidOperationException(
                    $"No entry for {typeof(TEntry).FullName}.");
            return entry;
        }

        protected async UniTask<TResult> RunFromFlowOut<TPayload, TResult>(
            TPayload payload, FlowOutPort flowOut, CancellationToken ct = default)
            where TPayload : class
        {
            ReleasePreviousFlow();
            var flow = Flow<TPayload, TResult>.Acquire(payload, this, ct);
            _lastFlow = flow;
            try
            {
                var dest = flowOut.Connection?.Destination;
                if (dest != null) await RunFromInPort(dest, flow);
                return flow.Result;
            }
            finally { flow.Complete(); }
        }

        // Used by ObserveVariable<T> on handle.Subscribe to drive a flow from the
        // observer's FlowOut. Internal to avoid widening the public surface for
        // arbitrary "drive a flow from X" needs — observers go through this seam.
        //
        // Observers are fire-and-forget at the call site (`runner.RunObserver(...).Forget()`),
        // so any unhandled exception in the spawned flow would otherwise be lost.
        // Catch and log here so failures show up immediately with their original
        // stack — without making the public surface awkward. Return type is
        // UniTaskVoid (instead of UniTask / Task) to mark this as fire-and-forget
        // and avoid the per-call Task allocation.
        internal async UniTaskVoid RunObserver<TPayload>(FlowOutPort flowOut, TPayload payload, CancellationToken ct = default)
            where TPayload : class
        {
            // Observers are fire-and-forget — caller never sees the Flow, so
            // we can return it to the pool immediately on completion (no
            // deferred-release contract needed). Don't touch _lastFlow.
            var flow = Flow<TPayload>.Acquire(payload, this, ct);
            try
            {
                var dest = flowOut.Connection?.Destination;
                if (dest != null) await RunFromInPort(dest, flow);
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogException(e);
            }
            finally
            {
                flow.Complete();
                flow.Release();
            }
        }

        async UniTask<TFlow> RunFromEntry<TFlow>(EntryRuntimeNodeBase entry, TFlow flow)
            where TFlow : Flow
        {
            try
            {
                var defaultOut = entry.GetDefaultOut();
                var dest = defaultOut.Connection?.Destination;
                if (dest != null) await RunFromInPort(dest, flow);
                return flow;
            }
            finally { flow.Complete(); }
            // Flow.Release() deferred — the caller still observes flow.Outcome
            // / flow.Result after this method returns. The next Run() on this
            // runner triggers ReleasePreviousFlow() which pushes this Flow
            // back to its typed pool. See lifetime contract on Flow class doc.
        }

        async UniTask RunFromInPort(FlowInPort start, Flow flow)
        {
            FlowInPort? current = start;
            while (current != null)
            {
                // Honor flow.Cancel() / flow.Return() even if the calling node
                // forgot to return null from its FlowIn handler. Returns null
                // are still the primary termination signal — this is the
                // belt-and-braces check so the runtime contract matches the
                // intuitive "Cancel stops execution" semantics.
                if (flow.Outcome != Outcome.Running) break;
                var next = await current.Invoke(flow);
                if (next == null) break;
                current = next.Connection?.Destination;
            }
        }
    }
}
