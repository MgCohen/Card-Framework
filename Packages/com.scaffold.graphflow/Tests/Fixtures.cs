#nullable enable
using System;
using System.Collections.Generic;
using Scaffold.GraphFlow;
using UnityEngine;

namespace Scaffold.GraphFlow.Tests
{
    public static class TestGraph
    {
        static readonly List<TestGraphAsset> _live = new();

        public static TestGraphAsset With(params RuntimeNode[] nodes)
        {
            var asset = ScriptableObject.CreateInstance<TestGraphAsset>();
            asset.nodes = new List<RuntimeNode>(nodes);
            for (var i = 0; i < nodes.Length; i++) nodes[i].nodeId = i + 1;
            _live.Add(asset);
            return asset;
        }

        public static void DestroyAll()
        {
            foreach (var a in _live)
                if (a != null) UnityEngine.Object.DestroyImmediate(a);
            _live.Clear();
        }
    }

    public static class GraphAssetWiring
    {
        public static TAsset Flow<TAsset>(this TAsset a, RuntimeNode from, string fromPort, RuntimeNode to, string toPort) where TAsset : GraphAsset
        {
            a.flowEdges.Add(new Edge { fromNodeId = from.nodeId, fromPortName = fromPort, toNodeId = to.nodeId, toPortName = toPort });
            return a;
        }

        public static TAsset Data<TAsset>(this TAsset a, RuntimeNode from, string fromPort, RuntimeNode to, string toPort) where TAsset : GraphAsset
        {
            a.connections.Add(new Edge { fromNodeId = from.nodeId, fromPortName = fromPort, toNodeId = to.nodeId, toPortName = toPort });
            return a;
        }
    }

    public interface IGraphLogSink
    {
        void Record(string message);
    }

    public sealed class CollectingLogSink : IGraphLogSink
    {
        readonly List<string> _messages = new();
        public IReadOnlyList<string> Messages => _messages;
        public void Record(string message) => _messages.Add(message);
    }

    public sealed class TestRunner : GraphRunner
    {
        public IGraphLogSink LogSink { get; }

        public TestRunner(BakedGraph baked, IGraphLogSink logSink) : base(baked)
        {
            LogSink = logSink;
        }
    }

    public sealed class TestBuilder : GraphBuilder<TestRunner>
    {
        readonly IGraphLogSink _logSink;
        public TestBuilder(IGraphLogSink logSink) { _logSink = logSink; }
        protected override TestRunner CreateRunner(BakedGraph baked) => new(baked, _logSink);
    }

    public sealed class TestEntry : IGraphEntry
    {
        public int Value;
    }

    [Serializable]
    public sealed class TestEntryRuntime : EntryRuntimeNode<TestEntry>
    {
        public FlowOutPort FlowOut;
        public OutputPort<int> Value;

        public TestEntryRuntime()
        {
            FlowOut = new FlowOutPort(this, nameof(FlowOut));
            Value = new OutputPort<int>(flow => PayloadOf(flow).Value);
            Ports.Add(FlowOut.Name, FlowOut);
            Ports.Add(nameof(Value), Value);
        }
    }

    [Serializable]
    public sealed class TestIntToStringRuntime : RuntimeNode
    {
        public InputPort<int> Value;
        public OutputPort<string> Result;

        public TestIntToStringRuntime()
        {
            Value = new InputPort<int>();
            Result = new OutputPort<string>(flow => Value.Read(flow).ToString());
            Ports.Add(nameof(Value), Value);
            Ports.Add(nameof(Result), Result);
        }
    }

    [Serializable]
    public sealed class TestLogDispatcherRuntime : RuntimeNode<TestRunner>
    {
        public FlowInPort FlowIn;
        public InputPort<string> Message;

        public TestLogDispatcherRuntime()
        {
            Message = new InputPort<string>();
            FlowIn = FlowInPort.Sync(this, nameof(FlowIn), flow =>
            {
                Runner(flow).LogSink.Record(Message.Read(flow) ?? "");
                return null;
            });
            Ports.Add(FlowIn.Name, FlowIn);
            Ports.Add(nameof(Message), Message);
        }
    }

    [Serializable]
    public sealed class TestEchoDispatcherRuntime : RuntimeNode<TestRunner>
    {
        public FlowInPort FlowIn;
        public FlowOutPort FlowOut;
        public InputPort<int> Magnitude;
        public OutputPort<string> Summary;

        public TestEchoDispatcherRuntime()
        {
            FlowOut = new FlowOutPort(this, nameof(FlowOut));
            Magnitude = new InputPort<int>();
            Summary = new OutputPort<string>(flow => $"echo:{Magnitude.Read(flow)}");
            FlowIn = FlowInPort.Sync(this, nameof(FlowIn), flow => FlowOut);
            Ports.Add(FlowIn.Name, FlowIn);
            Ports.Add(FlowOut.Name, FlowOut);
            Ports.Add(nameof(Magnitude), Magnitude);
            Ports.Add(nameof(Summary), Summary);
        }
    }

    public sealed class TestGraphAsset : GraphAsset<TestRunner> { }
}
