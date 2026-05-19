#nullable enable
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;

namespace Scaffold.GraphFlow.Tests
{
    public sealed class VariableEdgeTests
    {
        sealed class BareRunner : GraphRunner
        {
            public BareRunner(BakedGraph baked) : base(baked) { }
        }

        sealed class BareBuilder : GraphBuilder<BareRunner>
        {
            protected override BareRunner CreateRunner(BakedGraph baked) => new(baked);
        }

        sealed class BareAsset : GraphAsset<BareRunner> { }

        public sealed class EmptyEntry : IGraphEntry { }

        [System.Serializable]
        public sealed class EntryWithDoubler : EntryRuntimeNode<EmptyEntry>
        {
            public FlowOutPort FlowOut;
            public EntryWithDoubler()
            {
                FlowOut = new FlowOutPort(this, nameof(FlowOut));
                Ports.Add(FlowOut.Name, FlowOut);
            }
        }

        [System.Serializable]
        public sealed class Doubler : RuntimeNode
        {
            public InputPort<int> In;
            public OutputPort<int> Out;
            public Doubler()
            {
                In  = new InputPort<int>();
                Out = new OutputPort<int>(flow => In.Read(flow) * 2);
                Ports.Add(nameof(In),  In);
                Ports.Add(nameof(Out), Out);
            }
        }

        [Test]
        public async Task VariableEdgeWiresInputPortToBag()
        {
            var asset = ScriptableObject.CreateInstance<BareAsset>();
            var entry = new EntryWithDoubler { nodeId = 1, editorGuid = "a" };
            var doubler = new Doubler { nodeId = 2, editorGuid = "b" };
            asset.nodes.Add(entry);
            asset.nodes.Add(doubler);
            asset.variables.Add(VariableTestHelpers.Var("speed", new BlackboardInt { value = 21 }));
            asset.variableEdges.Add(new VariableEdge
            {
                variableId = "speed",
                toNodeId = 2,
                toPortName = nameof(Doubler.In),
            });

            var runner = new BareBuilder().Build(asset);
            var flow = await runner.Run(new EmptyEntry());

            Assert.AreEqual(42, doubler.Out.Read(flow));
        }

        [Test]
        public async Task SettingVariableAfterBuildFlowsThroughToOutput()
        {
            var asset = ScriptableObject.CreateInstance<BareAsset>();
            asset.nodes.Add(new EntryWithDoubler { nodeId = 1, editorGuid = "a" });
            var doubler = new Doubler { nodeId = 2, editorGuid = "b" };
            asset.nodes.Add(doubler);
            asset.variables.Add(VariableTestHelpers.Var("speed", new BlackboardInt { value = 1 }));
            asset.variableEdges.Add(new VariableEdge
            {
                variableId = "speed",
                toNodeId = 2,
                toPortName = nameof(Doubler.In),
            });

            var runner = new BareBuilder().Build(asset);
            Assert.IsTrue(runner.Variables.TryGet<int>("speed", out var handle));
            handle.Set(50);

            var flow = await runner.Run(new EmptyEntry());
            Assert.AreEqual(100, doubler.Out.Read(flow));
        }
    }
}
