using Framework.Tags;
using NUnit.Framework;

namespace Framework.Entities.Tests
{
    public class EntityTests
    {
        [Test]
        public void Construction_SetsIdTypeAndDefinitionId()
        {
            var stack = new TestStack();
            var entity = stack.EntityStore.Insert(EntityTypes.Card, 101);

            Assert.AreEqual(1, entity.Id);
            Assert.AreEqual(EntityTypes.Card, entity.Type);
            Assert.AreEqual(101, entity.DefinitionId);
        }

        [Test]
        public void SetGetRoundTrip()
        {
            var stack = new TestStack();
            var entity = stack.EntityStore.Insert(EntityTypes.Card);
            var health = new TagKey<int>(9200);

            entity.Set(health, 30);
            Assert.AreEqual(30, entity.Get(health));
        }

        [Test]
        public void HasAndRemove()
        {
            var stack = new TestStack();
            var entity = stack.EntityStore.Insert(EntityTypes.Card);
            var health = new TagKey<int>(9201);

            Assert.IsFalse(entity.Has(health));
            entity.Set(health, 5);
            Assert.IsTrue(entity.Has(health));
            Assert.IsTrue(entity.Remove(health));
            Assert.IsFalse(entity.Has(health));
            Assert.IsFalse(entity.Remove(health), "Second remove is a no-op");
        }

        [Test]
        public void MultipleKeys_AreIndependent()
        {
            var stack = new TestStack();
            var entity = stack.EntityStore.Insert(EntityTypes.Card);

            entity.Set(SampleTags.Health, 30);
            entity.Set(SampleTags.Attack, 7);
            entity.Set(SampleTags.IsCard, true);

            Assert.AreEqual(30,   entity.Get(SampleTags.Health));
            Assert.AreEqual(7,    entity.Get(SampleTags.Attack));
            Assert.AreEqual(true, entity.Get(SampleTags.IsCard));
        }

        [Test]
        public void Set_SameValue_DoesNotEmitDuplicateEvent()
        {
            // Equality short-circuit: writing the same value should not re-emit.
            var stack = new TestStack();
            var entity = stack.EntityStore.Insert(EntityTypes.Card);

            entity.Set(SampleTags.Health, 30);  // emits TagSetEvent
            var countAfterFirst = stack.Stream.Count;

            entity.Set(SampleTags.Health, 30);  // same value — should not emit
            Assert.AreEqual(countAfterFirst, stack.Stream.Count, "Same-value set must not emit");
        }

        [Test]
        public void Set_ChangedValue_EmitsTagChangeEvent()
        {
            var stack = new TestStack();
            var entity = stack.EntityStore.Insert(EntityTypes.Card);

            entity.Set(SampleTags.Health, 30);  // first set
            entity.Set(SampleTags.Health, 40);  // change

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
            var entity = stack.EntityStore.Insert(EntityTypes.Card);
            Assert.AreEqual("Entity#1", entity.ToString());
        }
    }
}
