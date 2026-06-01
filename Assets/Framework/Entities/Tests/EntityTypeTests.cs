using NUnit.Framework;

namespace Framework.Entities.Tests
{
    // EntityType is now a record over TypeKey — identity is (record type, Slot), not a Name
    // field. These cover the record semantics the rest of the feature relies on.
    public class EntityTypeTests
    {
        [Test]
        public void SameKind_AreEqual()
        {
            Assert.AreEqual(new Card(), new Card());
        }

        [Test]
        public void DifferentKind_AreNotEqual()
        {
            Assert.AreNotEqual(new Card(), new Zone());
        }

        [Test]
        public void Slot_MatchesDeclaration()
        {
            Assert.AreEqual(4, new Card().Slot);
            Assert.AreEqual(0, new Unknown().Slot);
        }

        [Test]
        public void ToString_IncludesTypeName()
        {
            // The record type name is the identity; ToString() is free and carries it.
            StringAssert.Contains("Card", new Card().ToString());
        }
    }
}
