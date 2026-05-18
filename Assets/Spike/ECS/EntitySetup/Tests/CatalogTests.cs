#nullable enable
using System;
using System.Linq;
using NUnit.Framework;

namespace Spike.EntitySetup.Tests
{
    public class CatalogTests
    {
        private static CardDefinition MakeMinion(int id, string name = "Test")
            => new(id, name, CardKind.Minion, Cost: 1, Attack: 1, Health: 1,
                   DivineShield: false, Effects: Array.Empty<CardEffect>());

        [Test]
        public void Add_ThenIndexerReturnsSameInstance()
        {
            var cat = new CatalogOf<CardDefinition>();
            var def = MakeMinion(42);
            cat.Add(def);
            Assert.That(cat[42], Is.SameAs(def));
        }

        [Test]
        public void Add_DuplicateId_Throws()
        {
            var cat = new CatalogOf<CardDefinition>();
            cat.Add(MakeMinion(7, "A"));
            Assert.Throws<InvalidOperationException>(() => cat.Add(MakeMinion(7, "B")));
        }

        [Test]
        public void TryGet_ReturnsFalseForUnknownId()
        {
            var cat = new CatalogOf<CardDefinition>();
            Assert.That(cat.TryGet(999, out var def), Is.False);
            Assert.That(def, Is.Null);
        }

        [Test]
        public void TryGet_ReturnsTrueAndDefForKnownId()
        {
            var cat = new CatalogOf<CardDefinition>();
            var d = MakeMinion(5);
            cat.Add(d);
            Assert.That(cat.TryGet(5, out var got), Is.True);
            Assert.That(got, Is.SameAs(d));
        }

        [Test]
        public void Count_TracksAddedDefinitions()
        {
            var cat = new CatalogOf<CardDefinition>();
            Assert.That(cat.Count, Is.EqualTo(0));
            cat.Add(MakeMinion(1));
            cat.Add(MakeMinion(2));
            Assert.That(cat.Count, Is.EqualTo(2));
        }

        [Test]
        public void All_EnumeratesEveryAddedDef()
        {
            var cat = new CatalogOf<CardDefinition>();
            var a = MakeMinion(1, "A");
            var b = MakeMinion(2, "B");
            cat.Add(a);
            cat.Add(b);
            CollectionAssert.AreEquivalent(new[] { a, b }, cat.All.ToList());
        }

        [Test]
        public void Catalogs_StartsWithEmptyCardsCatalog()
        {
            var c = new Catalogs();
            Assert.That(c.Cards, Is.Not.Null);
            Assert.That(c.Cards.Count, Is.EqualTo(0));
        }
    }
}
