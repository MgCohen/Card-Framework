#nullable enable
using System;
using NUnit.Framework;
using Scaffold.GraphFlow.Editor;
using Scaffold.GraphFlow.Nodes;
using UnityEngine;

namespace Scaffold.GraphFlow.EditorTests
{
    public sealed class GraphResultValidatorTests
    {
        // Minimal concrete GraphAsset for in-memory test construction.
        sealed class ValidatorTestAsset : GraphAsset { }

        sealed class TestPayload { }
        sealed class TestPayloadOther { }

        // Spell hierarchy for the polymorphism case.
        class Spell { }
        sealed class FireSpell : Spell { }
        sealed class Ball { }  // unrelated to Spell

        [Serializable]
        sealed class PayloadEntry : EntryRuntimeNode<TestPayload>
        {
            public FlowOutPort FlowOut;

            public PayloadEntry()
            {
                FlowOut = new FlowOutPort(this, nameof(FlowOut));
                Ports.Add(FlowOut.Name, FlowOut);
            }
        }

        ValidatorTestAsset BuildAsset(params RuntimeNode[] nodes)
        {
            var asset = ScriptableObject.CreateInstance<ValidatorTestAsset>();
            asset.name = "TestGraph";
            for (var i = 0; i < nodes.Length; i++)
            {
                nodes[i].nodeId = i + 1;
                asset.nodes.Add(nodes[i]);
            }
            return asset;
        }

        [TearDown]
        public void TearDown() { /* assets destroyed inline by each test */ }

        [Test]
        public void Validate_NoReturns_NoDiagnostics()
        {
            var entry = new PayloadEntry();
            var asset = BuildAsset(entry);

            try
            {
                var diags = GraphResultValidator.Validate(asset);
                Assert.IsEmpty(diags);
            }
            finally { ScriptableObject.DestroyImmediate(asset); }
        }

        [Test]
        public void Validate_SingleReturn_NoDiagnostics()
        {
            var entry = new PayloadEntry();
            var ret = new Return<int>();
            var asset = BuildAsset(entry, ret);
            asset.flowEdges.Add(new Edge { fromNodeId = entry.nodeId, fromPortName = "FlowOut", toNodeId = ret.nodeId, toPortName = "In" });

            try
            {
                var diags = GraphResultValidator.Validate(asset);
                Assert.IsEmpty(diags);
            }
            finally { ScriptableObject.DestroyImmediate(asset); }
        }

        [Test]
        public void Validate_TwoCompatibleReturns_SameType_NoDiagnostics()
        {
            var entry = new PayloadEntry();
            var a = new Return<int>();
            var b = new Return<int>();
            var asset = BuildAsset(entry, a, b);
            asset.flowEdges.Add(new Edge { fromNodeId = entry.nodeId, fromPortName = "FlowOut", toNodeId = a.nodeId, toPortName = "In" });
            asset.flowEdges.Add(new Edge { fromNodeId = entry.nodeId, fromPortName = "FlowOut", toNodeId = b.nodeId, toPortName = "In" });

            try
            {
                var diags = GraphResultValidator.Validate(asset);
                Assert.IsEmpty(diags);
            }
            finally { ScriptableObject.DestroyImmediate(asset); }
        }

        [Test]
        public void Validate_TwoCompatibleReturns_SubtypeRelationship_NoDiagnostics()
        {
            // Return<Spell> and Return<FireSpell> — FireSpell IS-A Spell, caller can use Run<*, Spell>.
            var entry = new PayloadEntry();
            var baseRet = new Return<Spell>();
            var derivedRet = new Return<FireSpell>();
            var asset = BuildAsset(entry, baseRet, derivedRet);
            asset.flowEdges.Add(new Edge { fromNodeId = entry.nodeId, fromPortName = "FlowOut", toNodeId = baseRet.nodeId, toPortName = "In" });
            asset.flowEdges.Add(new Edge { fromNodeId = entry.nodeId, fromPortName = "FlowOut", toNodeId = derivedRet.nodeId, toPortName = "In" });

            try
            {
                var diags = GraphResultValidator.Validate(asset);
                Assert.IsEmpty(diags, "Subtype relationship between returns should not warn.");
            }
            finally { ScriptableObject.DestroyImmediate(asset); }
        }

        [Test]
        public void Validate_TwoUnrelatedReturns_EmitsDiagnostic()
        {
            // Return<Spell> and Return<Ball> — unrelated types, no caller can satisfy both.
            var entry = new PayloadEntry();
            var spellRet = new Return<Spell>();
            var ballRet = new Return<Ball>();
            var asset = BuildAsset(entry, spellRet, ballRet);
            asset.flowEdges.Add(new Edge { fromNodeId = entry.nodeId, fromPortName = "FlowOut", toNodeId = spellRet.nodeId, toPortName = "In" });
            asset.flowEdges.Add(new Edge { fromNodeId = entry.nodeId, fromPortName = "FlowOut", toNodeId = ballRet.nodeId, toPortName = "In" });

            try
            {
                var diags = GraphResultValidator.Validate(asset);
                Assert.AreEqual(1, diags.Count, "One pairwise warning expected.");
                Assert.AreEqual(entry.nodeId, diags[0].EntryNodeId);
                StringAssert.Contains("Spell", diags[0].Message);
                StringAssert.Contains("Ball", diags[0].Message);
            }
            finally { ScriptableObject.DestroyImmediate(asset); }
        }

        [Test]
        public void Validate_ReturnReachableThroughIntermediateNode_StillDetected()
        {
            // entry -> Loop -> Return<int>
            // Validator must walk past non-Return nodes.
            var entry = new PayloadEntry();
            var loop = new Loop();
            var ret = new Return<int>();
            var asset = BuildAsset(entry, loop, ret);
            asset.flowEdges.Add(new Edge { fromNodeId = entry.nodeId, fromPortName = "FlowOut", toNodeId = loop.nodeId, toPortName = "Begin" });
            asset.flowEdges.Add(new Edge { fromNodeId = loop.nodeId, fromPortName = "Done", toNodeId = ret.nodeId, toPortName = "In" });

            try
            {
                var diags = GraphResultValidator.Validate(asset);
                // Single Return — no diagnostic, but walker should have found it.
                // Add a second incompatible Return to force the warning, validating reach.
                var ret2 = new Return<string> { nodeId = 99 };
                asset.nodes.Add(ret2);
                asset.flowEdges.Add(new Edge { fromNodeId = loop.nodeId, fromPortName = "Done", toNodeId = ret2.nodeId, toPortName = "In" });

                diags = GraphResultValidator.Validate(asset);
                Assert.AreEqual(1, diags.Count);
            }
            finally { ScriptableObject.DestroyImmediate(asset); }
        }
    }
}
