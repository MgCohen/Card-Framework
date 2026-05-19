#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using NUnit.Framework;
using Scaffold.Variables;
using UnityEngine;

namespace Scaffold.GraphFlow.Tests
{
    // Verifies Invariant 2: a GraphRunner subclass that overrides CreateVariableBag
    // can fully replace the storage used for graph-declared variables — the seam
    // is "construct the bag", not "chain a parent". The supplied bag arrives as
    // runner.Variables verbatim.
    public sealed class CreateVariableBagOverrideTests
    {
        sealed class CapturingBag : IVariableBag
        {
            public List<string> SeededIds { get; } = new();
            public IVariableBag? Parent => null;

            public bool TryGet<T>(string id, [MaybeNullWhen(false)] out IVariableHandle<T> handle)
            { handle = null; return false; }

            public bool TryGet(string id, [MaybeNullWhen(false)] out IVariableHandle handle)
            { handle = null; return false; }

            public IEnumerable<IVariableHandle> LocalHandles => Array.Empty<IVariableHandle>();
        }

        sealed class CustomBagRunner : GraphRunner
        {
            readonly CapturingBag _bag;
            public CustomBagRunner(BakedGraph baked, CapturingBag bag) : base(baked) { _bag = bag; }

            protected override IVariableBag CreateVariableBag(IEnumerable<RuntimeVariable> seed)
            {
                foreach (var v in seed)
                    if (v != null && !string.IsNullOrEmpty(v.id))
                        _bag.SeededIds.Add(v.id);
                return _bag;
            }
        }

        sealed class CustomBagBuilder : GraphBuilder<CustomBagRunner>
        {
            readonly CapturingBag _bag;
            public CustomBagBuilder(CapturingBag bag) { _bag = bag; }
            protected override CustomBagRunner CreateRunner(BakedGraph baked) => new(baked, _bag);
        }

        sealed class CustomBagAsset : GraphAsset<CustomBagRunner> { }

        public sealed class EmptyEntry : IGraphEntry { }

        [System.Serializable]
        public sealed class Entry : EntryRuntimeNode<EmptyEntry>
        {
            public FlowOutPort FlowOut;
            public Entry()
            {
                FlowOut = new FlowOutPort(this, nameof(FlowOut));
                Ports.Add(FlowOut.Name, FlowOut);
            }
        }

        [Test]
        public void OverrideReturnsBagInstanceUsedByRunner()
        {
            var bag = new CapturingBag();
            var asset = ScriptableObject.CreateInstance<CustomBagAsset>();
            asset.nodes.Add(new Entry { nodeId = 1, editorGuid = "a" });
            asset.variables.Add(VariableTestHelpers.Var("hp",   new BlackboardInt    { value = 1 }));
            asset.variables.Add(VariableTestHelpers.Var("name", new BlackboardString { value = "x" }));

            var runner = new CustomBagBuilder(bag).Build(asset);

            // Storage is exactly the bag the override returned.
            Assert.AreSame(bag, runner.Variables);

            // The override saw every declared variable in the seed.
            CollectionAssert.AreEquivalent(new[] { "hp", "name" }, bag.SeededIds);
        }
    }
}
