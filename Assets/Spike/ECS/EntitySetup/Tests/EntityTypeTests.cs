#nullable enable
using NUnit.Framework;

namespace Spike.EntitySetup.Tests
{
    public class EntityTypeTests
    {
        [Test]
        public void Equality_IsValueBased_OnSlotOnly()
        {
            var a = new EntityType(7, "Token");
            var b = new EntityType(7, "DifferentLabel");
            var c = new EntityType(8, "Token");
            Assert.That(a, Is.EqualTo(b));
            Assert.That(a.GetHashCode(), Is.EqualTo(b.GetHashCode()));
            Assert.That(a, Is.Not.EqualTo(c));
        }

        [Test]
        public void BundledTypes_HaveStableValuesAndNames()
        {
            Assert.That(EntityTypes.Card.Value,     Is.EqualTo(1));
            Assert.That(EntityTypes.Zone.Value,     Is.EqualTo(2));
            Assert.That(EntityTypes.Player.Value,   Is.EqualTo(3));
            Assert.That(EntityTypes.Modifier.Value, Is.EqualTo(4));

            Assert.That(EntityTypes.Card.Name,     Is.EqualTo("Card"));
            Assert.That(EntityTypes.Zone.Name,     Is.EqualTo("Zone"));
            Assert.That(EntityTypes.Player.Name,   Is.EqualTo("Player"));
            Assert.That(EntityTypes.Modifier.Name, Is.EqualTo("Modifier"));
        }

        [Test]
        public void BundledTypes_AreAllDistinct()
        {
            Assert.That(EntityTypes.Card,   Is.Not.EqualTo(EntityTypes.Zone));
            Assert.That(EntityTypes.Zone,   Is.Not.EqualTo(EntityTypes.Player));
            Assert.That(EntityTypes.Player, Is.Not.EqualTo(EntityTypes.Modifier));
        }

        [Test]
        public void ToString_UsesName()
        {
            Assert.That(EntityTypes.Card.ToString(), Is.EqualTo("Card"));
        }
    }
}
