using System;
using System.Collections.Generic;

namespace Scaffold.GraphFlow
{
    [Serializable]
    public abstract class RuntimeNode
    {
        public int nodeId;
        public string editorGuid = string.Empty;

        [NonSerialized] public readonly Dictionary<string, Port> Ports = new();

        internal virtual void Build(in NodeBuildSlice slice)
        {
            for (int i = 0; i < slice.Data.Count; i++)
            {
                var d = slice.Data[i];
                d.Destination.ConnectFrom(d.Source);
            }
            for (int i = 0; i < slice.Flow.Count; i++)
            {
                var f = slice.Flow[i];
                f.Source.Connection = f.Connection;
                f.Destination.Connection = f.Connection;
            }
        }

        public virtual void Initialize(GraphRunner runner) { }
    }

    [Serializable]
    public abstract class RuntimeNode<TRunner> : RuntimeNode where TRunner : GraphRunner
    {
        // Cached at Initialize so node code can dispatch through Runner without
        // re-casting flow.Runner on every FlowInPort fire. One runner per baked
        // graph per builder lifetime, so this is safe to capture here.
        [NonSerialized] TRunner _runner = null!;

        // Instance overload — preferred; ignores `flow` and returns the cached
        // typed runner. Kept as `Runner(flow)` instead of a no-arg property to
        // stay source-compatible with existing nodes (Strike500Dispatcher,
        // TestLogDispatcherRuntime, etc.) that call `Runner(flow)`.
        protected TRunner Runner(Flow flow) => _runner;

        public sealed override void Initialize(GraphRunner runner)
        {
            _runner = (TRunner)runner;
            Initialize(_runner);
        }
        public virtual void Initialize(TRunner runner) { }
    }
}
