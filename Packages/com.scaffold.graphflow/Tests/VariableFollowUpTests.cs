#nullable enable
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using Scaffold.Variables;
using UnityEngine;

namespace Scaffold.GraphFlow.Tests
{
    public sealed class VariableFollowUpTests
    {
        sealed class BareRunner : GraphRunner { public BareRunner(BakedGraph baked) : base(baked) { } }
        sealed class BareBuilder : GraphBuilder<BareRunner> { protected override BareRunner CreateRunner(BakedGraph baked) => new(baked); }
        sealed class BareAsset : GraphAsset<BareRunner> { }

        sealed class ParentedRunner : GraphRunner
        {
            readonly IVariableBag? _parent;
            public ParentedRunner(BakedGraph baked, IVariableBag? parent) : base(baked) { _parent = parent; }
            protected override IVariableBag CreateVariableBag(IEnumerable<RuntimeVariable> seed)
                => CreateInMemoryBag(seed, parent: _parent);
        }

        sealed class ParentedBuilder : GraphBuilder<ParentedRunner>
        {
            readonly IVariableBag? _parent;
            public ParentedBuilder(IVariableBag? parent) { _parent = parent; }
            protected override ParentedRunner CreateRunner(BakedGraph baked) => new(baked, _parent);
        }

        sealed class ParentedAsset : GraphAsset<ParentedRunner> { }

        public sealed class EmptyEntry : IGraphEntry { }

        [Serializable]
        public sealed class Entry : EntryRuntimeNode<EmptyEntry>
        {
            public FlowOutPort FlowOut;
            public Entry()
            {
                FlowOut = new FlowOutPort(this, nameof(FlowOut));
                Ports.Add(FlowOut.Name, FlowOut);
            }
        }

        [Serializable]
        public sealed class Doubler : RuntimeNode
        {
            public InputPort<int> In = null!;
            public OutputPort<int> Out = null!;
            public Doubler()
            {
                In = new InputPort<int>();
                Out = new OutputPort<int>(flow => In.Read(flow) * 2);
                Ports.Add(nameof(In), In);
                Ports.Add(nameof(Out), Out);
            }
        }

        // Item 8: Cycle detection — a parent chain that loops back to itself
        // terminates instead of stack-overflowing. Uses reflection to close
        // the loop since the public API doesn't allow cyclic parents.
        [Test]
        public void CyclicParentChainDoesNotHang()
        {
            var a = new InMemoryVariableBag();
            a.Add(new InMemoryHandle<int>("x", 1));
            var b = new InMemoryVariableBag(parent: a);

            // Forcibly create a cycle: a.Parent → b → a → b → ...
            var parentField = typeof(InMemoryVariableBag).GetField("<Parent>k__BackingField",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.IsNotNull(parentField, "Could not find Parent backing field for cycle test.");
            parentField!.SetValue(a, b);

            // "x" lives in bag `a`; looking up from `b` walks b → a and finds it.
            Assert.IsTrue(b.TryGet<int>("x", out var handle));
            Assert.AreEqual(1, handle.Value);

            // A key that exists nowhere must return false, not stack-overflow.
            Assert.IsFalse(b.TryGet<int>("missing", out _));

            // Non-generic overload also terminates on cycles.
            Assert.IsFalse(b.TryGet("missing", out _));
        }

        // Item 9: When a VariableEdge references a variableId that doesn't exist in
        // the bag (e.g. the declaration was removed), WireVariableEdges skips the edge
        // and the input port stays unconnected — reads return default(T).
        [Test]
        public async Task UnresolvedVariableEdgeLeavesPortReturningDefault()
        {
            var asset = ScriptableObject.CreateInstance<BareAsset>();
            var entry = new Entry { nodeId = 1, editorGuid = "a" };
            var doubler = new Doubler { nodeId = 2, editorGuid = "b" };
            var rec = new IntRecorder { nodeId = 3, editorGuid = "c" };
            asset.nodes.Add(entry);
            asset.nodes.Add(doubler);
            asset.nodes.Add(rec);

            // Declare NO variables — intentionally leave "ghost" unresolved.
            asset.variableEdges.Add(new VariableEdge
            {
                variableId = "ghost",
                toNodeId = 2,
                toPortName = nameof(Doubler.In),
            });

            asset.connections.Add(new Edge { fromNodeId = 2, fromPortName = nameof(Doubler.Out), toNodeId = 3, toPortName = nameof(IntRecorder.Value) });
            asset.flowEdges.Add(new Edge { fromNodeId = 1, fromPortName = nameof(Entry.FlowOut), toNodeId = 3, toPortName = nameof(IntRecorder.In) });

            var runner = new BareBuilder().Build(asset);
            await runner.Run(new EmptyEntry());

            // Unconnected InputPort<int>.Read returns default(int) = 0, doubled = 0.
            Assert.AreEqual(new[] { 0 }, rec.Recorded);
        }

        // Item 11: Building the same asset twice with independent runners must not
        // corrupt either runner's variable state. Item 1 removed the BakedGraph
        // cache from GraphBuilder — this test guards against regression.
        [Test]
        public void MultiBuild_SameAsset_IndependentVariableState()
        {
            var asset = ScriptableObject.CreateInstance<ParentedAsset>();
            asset.nodes.Add(new Entry { nodeId = 1, editorGuid = "a" });
            asset.variables.Add(VariableTestHelpers.Var("hp", new BlackboardInt { value = 10 }));

            var builder = new ParentedBuilder(parent: null);
            var runner1 = builder.Build(asset);
            var runner2 = builder.Build(asset);

            // Both runners start with the same default.
            Assert.IsTrue(runner1.Variables.TryGet<int>("hp", out var handle1));
            Assert.IsTrue(runner2.Variables.TryGet<int>("hp", out var handle2));
            Assert.AreEqual(10, handle1.Value);
            Assert.AreEqual(10, handle2.Value);

            // Mutating one runner's handle must not affect the other.
            handle1.Set(99);
            Assert.AreEqual(99, handle1.Value);
            Assert.AreEqual(10, handle2.Value);

            handle2.Set(42);
            Assert.AreEqual(99, handle1.Value);
            Assert.AreEqual(42, handle2.Value);
        }
    }
}
