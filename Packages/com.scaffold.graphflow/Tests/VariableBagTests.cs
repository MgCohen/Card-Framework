using System.Collections.Generic;
using NUnit.Framework;
using Scaffold.Variables;

namespace Scaffold.GraphFlow.Tests
{
    public sealed class VariableBagTests
    {
        static IEnumerable<(string id, VariableDefault? @default)> Seed(params (string id, VariableDefault def)[] entries)
        {
            foreach (var (id, def) in entries)
                yield return (id, def);
        }

        [Test]
        public void SeedsTypedHandlesFromDefaults()
        {
            var bag = new InMemoryVariableBag(Seed(
                ("hp",   new BlackboardInt   { value = 7 }),
                ("name", new BlackboardString{ value = "alice" })));

            Assert.IsTrue(bag.TryGet<int>("hp", out var hp));
            Assert.AreEqual(7, hp.Value);

            Assert.IsTrue(bag.TryGet<string>("name", out var name));
            Assert.AreEqual("alice", name.Value);
        }

        [Test]
        public void TypeMismatchReturnsFalse()
        {
            var bag = new InMemoryVariableBag(Seed(("hp", new BlackboardInt { value = 1 })));
            Assert.IsFalse(bag.TryGet<float>("hp", out _));
        }

        [Test]
        public void MissingKeyReturnsFalseAtRoot()
        {
            var bag = new InMemoryVariableBag();
            Assert.IsFalse(bag.TryGet<int>("missing", out _));
        }

        [Test]
        public void LookupCascadesThroughParents()
        {
            var global = new InMemoryVariableBag(Seed(("score", new BlackboardInt { value = 100 })));
            var graph  = new InMemoryVariableBag(Seed(("hp",    new BlackboardInt { value = 5 })),   global);
            var flow   = new InMemoryVariableBag(graph);

            Assert.IsTrue(flow.TryGet<int>("hp",    out var hp));     Assert.AreEqual(5,   hp.Value);
            Assert.IsTrue(flow.TryGet<int>("score", out var score));  Assert.AreEqual(100, score.Value);
        }

        [Test]
        public void WriteHitsOwningLayerImplicitly()
        {
            // Cached handle ref means writes target the bag that owns the id —
            // no explicit "find owner" walk on the hot path.
            var global = new InMemoryVariableBag(Seed(("score", new BlackboardInt { value = 100 })));
            var graph  = new InMemoryVariableBag(Seed(("hp",    new BlackboardInt { value = 5 })),   global);
            var flow   = new InMemoryVariableBag(graph);

            Assert.IsTrue(flow.TryGet<int>("score", out var scoreFromFlow));
            scoreFromFlow.Set(999);

            // Reading from the global bag sees the write.
            Assert.IsTrue(global.TryGet<int>("score", out var scoreFromGlobal));
            Assert.AreEqual(999, scoreFromGlobal.Value);

            // Graph layer never declared "score", so it doesn't shadow.
            Assert.IsTrue(graph.TryGet<int>("score", out var scoreFromGraph));
            Assert.AreSame(scoreFromGlobal, scoreFromGraph);
        }

        [Test]
        public void SubscribeFiresOnDistinctValueOnly()
        {
            var bag = new InMemoryVariableBag(Seed(("hp", new BlackboardInt { value = 0 })));
            Assert.IsTrue(bag.TryGet<int>("hp", out var hp));

            int fires = 0;
            int last = -1;
            hp.Subscribe(v => { fires++; last = v; });

            hp.Set(0);   // no-op
            hp.Set(5);   // change
            hp.Set(5);   // no-op
            hp.Set(7);   // change

            Assert.AreEqual(2, fires);
            Assert.AreEqual(7, last);
        }

        [Test]
        public void NonGenericTryGetReturnsBaseHandle()
        {
            var bag = new InMemoryVariableBag(Seed(("hp", new BlackboardInt { value = 3 })));
            Assert.IsTrue(bag.TryGet("hp", out var raw));
            Assert.AreEqual(typeof(int), raw.Type);
            Assert.AreEqual("hp", raw.Id);
        }
    }
}
