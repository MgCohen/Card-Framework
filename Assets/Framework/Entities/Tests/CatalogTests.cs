using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Framework.Entities.Tests
{
    public class CatalogTests
    {
        [Test]
        public void Add_RegistersDef()
        {
            var catalog = new Catalog<CardTestDef>();
            var def = new CardTestDef(101, 3);

            catalog.Add(def);
            Assert.AreEqual(def, catalog.ById(101));
        }

        [Test]
        public void ById_UnknownId_Throws()
        {
            var catalog = new Catalog<CardTestDef>();
            Assert.Throws<KeyNotFoundException>(() => catalog.ById(999));
        }

        [Test]
        public void TryGetById_ReturnsTrueForKnown()
        {
            var catalog = new Catalog<CardTestDef>();
            var def = new CardTestDef(101, 3);
            catalog.Add(def);

            Assert.IsTrue(catalog.TryGetById(101, out var fetched));
            Assert.AreEqual(def, fetched);
        }

        [Test]
        public void TryGetById_ReturnsFalseForUnknown()
        {
            var catalog = new Catalog<CardTestDef>();
            Assert.IsFalse(catalog.TryGetById(999, out _));
        }

        [Test]
        public void GetId_ReturnsRegisteredId()
        {
            var catalog = new Catalog<CardTestDef>();
            var def = new CardTestDef(101, 3);
            catalog.Add(def);

            Assert.AreEqual(101, catalog.GetId(def));
        }

        [Test]
        public void GetId_UnregisteredDef_Throws()
        {
            var catalog = new Catalog<CardTestDef>();
            var def = new CardTestDef(101, 3);

            Assert.Throws<KeyNotFoundException>(() => catalog.GetId(def));
        }

        [Test]
        public void Add_DuplicateId_Throws()
        {
            var catalog = new Catalog<CardTestDef>();
            catalog.Add(new CardTestDef(101, 3));

            Assert.Throws<InvalidOperationException>(() => catalog.Add(new CardTestDef(101, 5)));
        }

        [Test]
        public void Add_ZeroId_Throws()
        {
            var catalog = new Catalog<CardTestDef>();
            Assert.Throws<InvalidOperationException>(() => catalog.Add(new CardTestDef(0, 3)));
        }
    }
}
