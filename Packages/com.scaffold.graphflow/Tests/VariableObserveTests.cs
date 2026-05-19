#nullable enable
using System.Threading.Tasks;
using NUnit.Framework;
using Scaffold.GraphFlow.Nodes;
using UnityEngine;

namespace Scaffold.GraphFlow.Tests
{
    public sealed class VariableObserveTests
    {
        sealed class BareRunner  : GraphRunner    { public BareRunner(BakedGraph baked) : base(baked) { } }
        sealed class BareBuilder : GraphBuilder<BareRunner> { protected override BareRunner CreateRunner(BakedGraph baked) => new(baked); }
        sealed class BareAsset   : GraphAsset<BareRunner> { }

        public sealed class EmptyEntry : IGraphEntry { }

        [Test]
        public async Task HandleChangeFiresObserverFlowWithNewValue()
        {
            var asset = ScriptableObject.CreateInstance<BareAsset>();
            var observe  = new ObserveVariable<int> { nodeId = 1, editorGuid = "a" };
            var recorder = new IntRecorder       { nodeId = 2, editorGuid = "b" };
            VariableTestHelpers.SetVariableId(observe, "hp");

            asset.nodes.Add(observe);
            asset.nodes.Add(recorder);
            asset.variables.Add(VariableTestHelpers.Var("hp", new BlackboardInt { value = 0 }));
            asset.flowEdges.Add(new Edge   { fromNodeId = 1, fromPortName = nameof(ObserveVariable<int>.FlowOut),  toNodeId = 2, toPortName = nameof(IntRecorder.In) });
            asset.connections.Add(new Edge { fromNodeId = 1, fromPortName = nameof(ObserveVariable<int>.NewValue), toNodeId = 2, toPortName = nameof(IntRecorder.Value) });

            var runner = new BareBuilder().Build(asset);
            Assert.IsTrue(runner.Variables.TryGet<int>("hp", out var handle));

            handle.Set(7);
            await Task.Yield();   // let any continuations land

            handle.Set(7);        // same — Subscribe callback not raised, no record
            handle.Set(11);
            await Task.Yield();

            Assert.AreEqual(new[] { 7, 11 }, recorder.Recorded);
        }
    }
}
