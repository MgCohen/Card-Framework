#nullable enable
using NUnit.Framework;

namespace Spike.EntitySetup.Tests
{
    public class TagBagTests
    {
        private static Entity NewEntity()
            => new EntityStore().Create(EntityTypes.Card);

        [Test]
        public void Has_ReturnsFalseUntilSet()
        {
            var e = NewEntity();
            Assert.That(e.Has(Tags.Attack), Is.False);
            e.Set(Tags.Attack, 3);
            Assert.That(e.Has(Tags.Attack), Is.True);
        }

        [Test]
        public void Get_ReturnsDefaultForMissingKey()
        {
            var e = NewEntity();
            Assert.That(e.Get(Tags.Attack), Is.EqualTo(0));
            Assert.That(e.Get(Tags.DivineShield), Is.False);
        }

        [Test]
        public void Set_StoresAndGetRetrievesValue()
        {
            var e = NewEntity();
            e.Set(Tags.Health, 30);
            Assert.That(e.Get(Tags.Health), Is.EqualTo(30));
        }

        [Test]
        public void Set_OverwritesPreviousValue()
        {
            var e = NewEntity();
            e.Set(Tags.Health, 30);
            e.Set(Tags.Health, 25);
            Assert.That(e.Get(Tags.Health), Is.EqualTo(25));
        }

        [Test]
        public void DifferentTypedKeys_DoNotCollide()
        {
            var e = NewEntity();
            e.Set(Tags.Attack, 4);
            e.Set(Tags.Health, 5);
            Assert.That(e.Get(Tags.Attack), Is.EqualTo(4));
            Assert.That(e.Get(Tags.Health), Is.EqualTo(5));
        }
    }
}
