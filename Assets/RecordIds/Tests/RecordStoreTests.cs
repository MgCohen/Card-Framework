using System.Collections.Generic;
using NUnit.Framework;

namespace Framework.RecordIds.Tests
{
    public class RecordStoreTests
    {
        [Test]
        public void Write_ThenById_ReturnsTheRecord()
        {
            var store = new TestStore();
            var record = new TestRecord(new AId { Value = 1 });

            store.Add(record);

            Assert.AreSame(record, store.ById(new AId { Value = 1 }));
            Assert.AreEqual(1, store.Count);
        }

        [Test]
        public void ById_OnMiss_Throws()
        {
            Assert.Throws<KeyNotFoundException>(() => new TestStore().ById(new AId { Value = 1 }));
        }

        [Test]
        public void TryGetById_OnMiss_ReturnsFalse()
        {
            Assert.IsFalse(new TestStore().TryGetById(new AId { Value = 1 }, out _));
        }

        [Test]
        public void EqualId_FromAnotherObject_FindsTheRecord()
        {
            var store = new TestStore();
            var record = new TestRecord(new AId { Value = 4 });
            store.Add(record);

            Assert.IsTrue(store.TryGetById(new AId { Value = 4 }, out var found));
            Assert.AreSame(record, found);
        }

        [Test]
        public void Erase_RemovesTheRecord()
        {
            var store = new TestStore();
            store.Add(new TestRecord(new AId { Value = 2 }));

            Assert.IsTrue(store.Remove(new AId { Value = 2 }));
            Assert.AreEqual(0, store.Count);
        }
    }
}
