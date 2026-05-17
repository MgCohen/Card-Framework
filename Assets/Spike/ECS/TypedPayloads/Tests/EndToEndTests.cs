#nullable enable
using NUnit.Framework;
using TypedPayloads.Engine;
using TypedPayloads.Sample;

namespace TypedPayloads.Tests
{
    /// <summary>
    /// Proves the whole pipeline: <c>Entity.Set</c> → internal event →
    /// <c>EntityStore</c> codec-encodes → wire event in the log →
    /// <c>MirrorWorld.Apply</c> stores the raw <see cref="TagValue"/> →
    /// consumer reads back through <see cref="MirrorEntity.Get{T}"/> which
    /// type-directed decodes via the codec.
    /// </summary>
    public class EndToEndTests
    {
        [OneTimeSetUp]
        public void Setup() => Codecs.Install();

        [Test]
        public void EntitySet_EmitsTagSetEvent_WithEncodedValue()
        {
            var log   = new EventLog();
            var store = new EntityStore(log);
            var e     = store.Create();

            e.Set(Tags.Health, 30);

            Assert.AreEqual(2, log.Count);                       // Created, Set
            Assert.IsInstanceOf<EntityCreatedEvent>(log[0]);
            var ts = (TagSetEvent)log[1];
            Assert.AreEqual(e.Id, ts.EntityId);
            Assert.AreEqual(Tags.Health.Slot, ts.Slot);
            Assert.AreEqual(TagValue.I64(30), ts.Value);
        }

        [Test]
        public void SecondSet_EmitsTagChangeEvent_WithPrevAndNext()
        {
            var log   = new EventLog();
            var store = new EntityStore(log);
            var e     = store.Create();

            e.Set(Tags.Health, 30);
            e.Set(Tags.Health, 24);

            Assert.AreEqual(3, log.Count);                       // Created, Set, Change
            var tc = (TagChangeEvent)log[2];
            Assert.AreEqual(TagValue.I64(30), tc.Prev);
            Assert.AreEqual(TagValue.I64(24), tc.Next);
        }

        [Test]
        public void NoopSet_DoesNotEmit()
        {
            var log   = new EventLog();
            var store = new EntityStore(log);
            var e     = store.Create();

            e.Set(Tags.Health, 30);
            int before = log.Count;
            e.Set(Tags.Health, 30);              // same value — must no-op
            Assert.AreEqual(before, log.Count);
        }

        [Test]
        public void Vector3Tag_RoundtripsThroughWireToMirror()
        {
            var log    = new EventLog();
            var store  = new EntityStore(log);
            var mirror = new MirrorWorld();
            var e      = store.Create();
            var pos    = new Vector3(1.0f, 2.5f, 3.0f);

            e.Set(Tags.Pos, pos);

            foreach (var ev in log) mirror.Apply(ev);

            var m = mirror.Get(e.Id);
            Assert.IsNotNull(m);
            Assert.AreEqual(pos, m!.Get(Tags.Pos));
        }

        [Test]
        public void MirrorApplyAll_ReachesHeadState_AllVariants()
        {
            var log    = new EventLog();
            var store  = new EntityStore(log);
            var mirror = new MirrorWorld();
            var e      = store.Create();

            e.Set(Tags.Health,   30);
            e.Set(Tags.IsMinion, true);
            e.Set(Tags.Pos,      new Vector3(5, 5, 5));
            e.Set(Tags.Health,   24);

            foreach (var ev in log) mirror.Apply(ev);

            var m = mirror.Get(e.Id)!;
            Assert.AreEqual(24,                       m.Get(Tags.Health));
            Assert.AreEqual(true,                     m.Get(Tags.IsMinion));
            Assert.AreEqual(new Vector3(5, 5, 5),     m.Get(Tags.Pos));
        }

        [Test]
        public void Unapply_OfFirstWrite_RemovesSlot()
        {
            var log    = new EventLog();
            var store  = new EntityStore(log);
            var mirror = new MirrorWorld();
            var e      = store.Create();

            e.Set(Tags.Health, 30);
            foreach (var ev in log) mirror.Apply(ev);

            Assert.IsTrue(mirror.Get(e.Id)!.Has(Tags.Health.Slot));

            // Unapply the TagSet — should remove the slot, not write default.
            mirror.Unapply(log[log.Count - 1]);
            Assert.IsFalse(mirror.Get(e.Id)!.Has(Tags.Health.Slot));
        }

        [Test]
        public void Unapply_OfTagChange_RestoresPrev()
        {
            var log    = new EventLog();
            var store  = new EntityStore(log);
            var mirror = new MirrorWorld();
            var e      = store.Create();

            e.Set(Tags.Health, 30);
            e.Set(Tags.Health, 24);
            foreach (var ev in log) mirror.Apply(ev);

            mirror.Unapply(log[log.Count - 1]);   // undo the change
            Assert.AreEqual(30, mirror.Get(e.Id)!.Get(Tags.Health));
        }

        [Test]
        public void EntityDestroy_EmitsAndMirrorRemoves()
        {
            var log    = new EventLog();
            var store  = new EntityStore(log);
            var mirror = new MirrorWorld();
            var e      = store.Create();

            store.Destroy(e);
            foreach (var ev in log) mirror.Apply(ev);

            Assert.IsNull(mirror.Get(e.Id));
        }

        [Test]
        public void TagRemove_EmitsAndMirrorStripsSlot()
        {
            var log    = new EventLog();
            var store  = new EntityStore(log);
            var mirror = new MirrorWorld();
            var e      = store.Create();

            e.Set(Tags.IsMinion, true);
            e.Remove(Tags.IsMinion);

            foreach (var ev in log) mirror.Apply(ev);

            Assert.IsFalse(mirror.Get(e.Id)!.Has(Tags.IsMinion.Slot));
        }
    }
}
