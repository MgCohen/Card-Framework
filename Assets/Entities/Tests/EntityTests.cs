using Framework.Tags;
using NUnit.Framework;

namespace Framework.Entities.Tests
{
    // Entity is data-only — tag access goes through TagService (server) or TagStore (client).
    // These tests exercise the server-side authoring path via TagService against an Entity id.
    // Entities are placed with an explicit-id store insert (no emit) so the stream carries only
    // the tag events under test; id allocation is covered in EntityServiceTests.
    public class EntityTests
    {
        [Test]
        public void Construction_SetsIdTypeAndDefinitionId()
        {
            var stack = new TestStack();
            var entity = stack.EntityStore.Insert(1, new Card(), 101);

            Assert.AreEqual(1, entity.Id);
            Assert.AreEqual(new Card(), entity.Type);
            Assert.AreEqual(101, entity.DefinitionId);
        }

        [Test]
        public void SetGetRoundTrip()
        {
            var stack = new TestStack();
            var entity = stack.EntityStore.Insert(1, new Card(), 0);
            var health = SampleTags.Health;

            stack.Tags.Set(entity.Id, health, 30);
            Assert.AreEqual(30, stack.Tags.Get(entity.Id, health));
        }

        [Test]
        public void HasAndRemove()
        {
            var stack = new TestStack();
            var entity = stack.EntityStore.Insert(1, new Card(), 0);
            var health = SampleTags.Attack;

            Assert.IsFalse(stack.Tags.Has(entity.Id, health));
            stack.Tags.Set(entity.Id, health, 5);
            Assert.IsTrue(stack.Tags.Has(entity.Id, health));
            Assert.IsTrue(stack.Tags.Remove(entity.Id, health));
            Assert.IsFalse(stack.Tags.Has(entity.Id, health));
            Assert.IsFalse(stack.Tags.Remove(entity.Id, health), "Second remove is a no-op");
        }

        [Test]
        public void MultipleKeys_AreIndependent()
        {
            var stack = new TestStack();
            var entity = stack.EntityStore.Insert(1, new Card(), 0);

            stack.Tags.Set(entity.Id, SampleTags.Health, 30);
            stack.Tags.Set(entity.Id, SampleTags.Attack, 7);
            stack.Tags.Set(entity.Id, SampleTags.IsCard, true);

            Assert.AreEqual(30,   stack.Tags.Get(entity.Id, SampleTags.Health));
            Assert.AreEqual(7,    stack.Tags.Get(entity.Id, SampleTags.Attack));
            Assert.AreEqual(true, stack.Tags.Get(entity.Id, SampleTags.IsCard));
        }

        [Test]
        public void Set_SameValue_DoesNotEmitDuplicateEvent()
        {
            // Equality short-circuit: writing the same value should not re-emit.
            var stack = new TestStack();
            var entity = stack.EntityStore.Insert(1, new Card(), 0);

            stack.Tags.Set(entity.Id, SampleTags.Health, 30);  // emits TagSetEvent
            var countAfterFirst = stack.Stream.Count;

            stack.Tags.Set(entity.Id, SampleTags.Health, 30);  // same value — should not emit
            Assert.AreEqual(countAfterFirst, stack.Stream.Count, "Same-value set must not emit");
        }

        [Test]
        public void Set_ChangedValue_EmitsTagChangeEvent()
        {
            var stack = new TestStack();
            var entity = stack.EntityStore.Insert(1, new Card(), 0);

            stack.Tags.Set(entity.Id, SampleTags.Health, 30);  // first set
            stack.Tags.Set(entity.Id, SampleTags.Health, 40);  // change

            // Last event should be TagChangeEvent<int> with Prev=30, Next=40
            Assert.AreEqual(2, stack.Stream.Count);
            Assert.IsInstanceOf<TagChangeEvent<int>>(stack.Stream[1]);
            var change = (TagChangeEvent<int>)stack.Stream[1];
            Assert.AreEqual(30, change.Prev);
            Assert.AreEqual(40, change.Next);
        }

        [Test]
        public void ToString_ContainsId()
        {
            var stack = new TestStack();
            var entity = stack.EntityStore.Insert(1, new Card(), 0);
            Assert.AreEqual("Entity#1", entity.ToString());
        }
    }
}
