#nullable enable
using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using Scaffold.Variables;
using UnityEngine;

namespace Scaffold.GraphFlow.Tests
{
    public sealed class VariableWiringTests
    {
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

        [System.Serializable]
        public sealed class CapturingEntry : EntryRuntimeNode<EmptyEntry>
        {
            public FlowOutPort FlowOut;
            public CapturingEntry()
            {
                FlowOut = new FlowOutPort(this, nameof(FlowOut));
                Ports.Add(FlowOut.Name, FlowOut);
            }
        }

        static ParentedAsset Asset(params (string id, VariableDefault def)[] vars)
        {
            var asset = ScriptableObject.CreateInstance<ParentedAsset>();
            asset.nodes.Add(new CapturingEntry { nodeId = 1, editorGuid = "a" });
            foreach (var (id, def) in vars)
                asset.variables.Add(VariableTestHelpers.Var(id, def));
            return asset;
        }

        [Test]
        public void RunnerVariablesSeededFromAsset()
        {
            var asset  = Asset(("hp", new BlackboardInt { value = 9 }));
            var runner = new ParentedBuilder(parent: null).Build(asset);

            Assert.IsNotNull(runner.Variables);
            Assert.IsNull(runner.Variables.Parent);
            Assert.IsTrue(runner.Variables.TryGet<int>("hp", out var hp));
            Assert.AreEqual(9, hp.Value);
        }

        [Test]
        public void RunnerParentBagComesFromCreateVariableBagOverride()
        {
            var global = new InMemoryVariableBag();
            global.Add(new InMemoryHandle<int>("score", 100));
            var asset  = Asset(("hp", new BlackboardInt { value = 5 }));
            var runner = new ParentedBuilder(global).Build(asset);

            Assert.AreSame(global, runner.Variables.Parent);
            Assert.IsTrue(runner.Variables.TryGet<int>("score", out var score));
            Assert.AreEqual(100, score.Value);
        }

        [Test]
        public async Task FlowVariablesParentChainsToRunner()
        {
            var asset  = Asset(("hp", new BlackboardInt { value = 1 }));
            var runner = new ParentedBuilder(parent: null).Build(asset);

            var flow = await runner.Run(new EmptyEntry());

            Assert.IsNotNull(flow.Variables);
            Assert.AreSame(runner.Variables, flow.Variables.Parent);
            Assert.IsTrue(flow.Variables.TryGet<int>("hp", out var hp));
            Assert.AreEqual(1, hp.Value);
        }

        [Test]
        public async Task SetThroughFlowHitsRunnerOwnedHandle()
        {
            var asset  = Asset(("hp", new BlackboardInt { value = 1 }));
            var runner = new ParentedBuilder(parent: null).Build(asset);
            var flow   = await runner.Run(new EmptyEntry());

            Assert.IsTrue(flow.Variables.TryGet<int>("hp", out var fromFlow));
            fromFlow.Set(99);

            Assert.IsTrue(runner.Variables.TryGet<int>("hp", out var fromRunner));
            Assert.AreSame(fromFlow, fromRunner);
            Assert.AreEqual(99, fromRunner.Value);
        }
    }
}
