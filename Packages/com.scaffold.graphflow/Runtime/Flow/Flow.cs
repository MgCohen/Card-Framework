#nullable enable
using System.Collections.Generic;
using System.Threading;
using Scaffold.Variables;

namespace Scaffold.GraphFlow
{
    public enum Outcome { Running, Returned, Cancelled }

    // Contravariant: a Flow<*, Spell> implements IFlowResult<FireSpell> too,
    // so Return<FireSpell> writes through correctly when the caller asked for
    // Spell. Unrelated types fail the cast and throw InvalidCastException.
    internal interface IFlowResult<in TResult>
    {
        void SetResult(TResult value);
    }

    // Per-type static pool. Each Flow<TPayload> and Flow<TPayload, TResult>
    // gets its own pool (one Stack per concrete type) — no dictionary lookup,
    // no boxing. Bounded so accumulation of distinct types can't grow forever;
    // typical app stabilises at 1-2 retained instances per type.
    internal static class FlowPool<TFlow> where TFlow : Flow
    {
        const int MaxRetained = 32;
        static readonly Stack<TFlow> _free = new();

        public static bool TryPop(out TFlow flow)
        {
            if (_free.Count == 0) { flow = null!; return false; }
            flow = _free.Pop();
            return true;
        }

        public static void Push(TFlow flow)
        {
            if (_free.Count >= MaxRetained) return;
            _free.Push(flow);
        }
    }

    /// <summary>
    /// Per-Run execution context. Pool-managed: instances are reused across
    /// Runs of the same (TPayload[, TResult]) type. <b>Lifetime contract</b> —
    /// a Flow returned from <c>await runner.Run(...)</c> is valid until the
    /// caller invokes <c>runner.Run(...)</c> with the same type combo again
    /// on this runner. Read <c>Outcome</c> / <c>Result</c> / <c>Variables</c>
    /// before launching another Run (typically: immediately after await),
    /// or copy them to local variables.
    /// </summary>
    public class Flow
    {
        IVariableBag? _variables;
        GraphRunner _runner = null!;

        public GraphRunner Runner => _runner;
        public CancellationToken Token { get; private set; }

        // Index into the runner's per-port caches; returned to the pool on Complete.
        public int Index { get; private set; }

        // Per-flow freshness key; OutputPort entries are stale when their Version differs.
        internal int CacheVersion { get; private set; }

        public IVariableBag Variables =>
            _variables ??= new InMemoryVariableBag(_runner.Variables);

        public Outcome Outcome { get; private set; } = Outcome.Running;

        internal Flow() { }

        internal void InitializeCore(GraphRunner runner, CancellationToken token)
        {
            _runner = runner;
            Token = token;
            Index = runner.AcquireFlowIndex();
            CacheVersion = runner.NextCacheVersion();
            Outcome = Outcome.Running;
            _variables = null;
        }

        public FlowOutPort? Return()
        {
            Outcome = Outcome.Returned;
            return null;
        }

        public FlowOutPort? Cancel()
        {
            Outcome = Outcome.Cancelled;
            return null;
        }

        public void InvalidateAll() => CacheVersion = _runner.NextCacheVersion();

        internal void Complete() => _runner.ReleaseFlowIndex(Index);

        // Each concrete subtype overrides to clear its typed reference fields
        // and push itself into its own typed pool. Base does nothing.
        internal virtual void Release() { }
    }

    public class Flow<TPayload> : Flow where TPayload : class
    {
        public TPayload Payload { get; private set; } = null!;

        internal Flow() { }

        internal void Initialize(TPayload payload, GraphRunner runner, CancellationToken token)
        {
            Payload = payload;
            InitializeCore(runner, token);
        }

        // Subclass helper: lets Flow<TPayload, TResult>.Release clear the
        // inherited TPayload reference without invoking the base pool path.
        internal void ClearPayload() => Payload = null!;

        internal static Flow<TPayload> Acquire(TPayload payload, GraphRunner runner, CancellationToken token)
        {
            if (!FlowPool<Flow<TPayload>>.TryPop(out var flow))
                flow = new Flow<TPayload>();
            flow.Initialize(payload, runner, token);
            return flow;
        }

        internal override void Release()
        {
            ClearPayload();
            FlowPool<Flow<TPayload>>.Push(this);
        }
    }

    public sealed class Flow<TPayload, TResult> : Flow<TPayload>, IFlowResult<TResult>
        where TPayload : class
    {
        public TResult Result { get; private set; } = default!;

        internal Flow() { }

        internal new void Initialize(TPayload payload, GraphRunner runner, CancellationToken token)
        {
            Result = default!;
            base.Initialize(payload, runner, token);
        }

        internal static new Flow<TPayload, TResult> Acquire(TPayload payload, GraphRunner runner, CancellationToken token)
        {
            if (!FlowPool<Flow<TPayload, TResult>>.TryPop(out var flow))
                flow = new Flow<TPayload, TResult>();
            flow.Initialize(payload, runner, token);
            return flow;
        }

        internal override void Release()
        {
            Result = default!;
            ClearPayload();
            FlowPool<Flow<TPayload, TResult>>.Push(this);
        }

        // Explicit implementation — Result is read-only from outside; only
        // Return<TResult> (via the IFlowResult cast) can write.
        void IFlowResult<TResult>.SetResult(TResult value) => Result = value;
    }
}
