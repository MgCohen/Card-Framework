using Framework.Wire;
using NUnit.Framework;

namespace Framework.Blocks.Tests
{
    public class BlockTests
    {
        [Test]
        public void NewBlock_HasPositionZero()
        {
            var block = new TestBlock();

            Assert.AreEqual(0, block.Position);
            Assert.AreEqual(new BlockId { Value = 0 }, block.Id);
        }

        [Test]
        public void Emit_SetsPositionAndId()
        {
            var stream = new EventStream();
            _ = new BlockPositions(stream);
            var block = new TestBlock();

            stream.Emit(block);

            Assert.AreEqual(1, block.Position);
            Assert.AreEqual(new BlockId { Value = 1 }, block.Id);
        }
    }
}
