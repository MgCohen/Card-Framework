using NUnit.Framework;

namespace Framework.RecordIds.Tests
{
    public class IdAllocatorTests
    {
        [Test]
        public void Next_StartsAtOne_AndCountsUp()
        {
            var ids = new IdAllocator<AId>();

            Assert.AreEqual(new AId { Value = 1 }, ids.Next());
            Assert.AreEqual(new AId { Value = 2 }, ids.Next());
            Assert.AreEqual(new AId { Value = 3 }, ids.Next());
        }

        [Test]
        public void Next_StartsAtFirst()
        {
            Assert.AreEqual(10, new IdAllocator<AId>(first: 10).Next().Value);
        }

        [Test]
        public void BaseRelease_KeepsTheIdUsed()
        {
            var ids = new IdAllocator<AId>();
            ids.Next();
            var last = ids.Next();

            ids.Release(last);

            Assert.AreEqual(3, ids.Next().Value);
        }

        [Test]
        public void StackRelease_OfLastId_GivesItBack()
        {
            var ids = new StackIdAllocator<AId>();
            ids.Next();
            var last = ids.Next();

            ids.Release(last);

            Assert.AreEqual(2, ids.Next().Value);
        }

        [Test]
        public void StackRelease_InReverseOrder_GivesAllBack()
        {
            var ids = new StackIdAllocator<AId>();
            var first = ids.Next();
            var second = ids.Next();

            ids.Release(second);
            ids.Release(first);

            Assert.AreEqual(1, ids.Next().Value);
        }

        [Test]
        public void StackRelease_OfMiddleId_LeavesAGap()
        {
            var ids = new StackIdAllocator<AId>();
            var first = ids.Next();
            ids.Next();

            ids.Release(first);

            Assert.AreEqual(3, ids.Next().Value);
        }

        [Test]
        public void StackRelease_BeforeAnyNext_DoesNothing()
        {
            var ids = new StackIdAllocator<AId>();

            ids.Release(new AId { Value = 0 });
            ids.Release(null);

            Assert.AreEqual(1, ids.Next().Value);
        }
    }
}
