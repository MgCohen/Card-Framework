using Framework.Wire;
using NUnit.Framework;

namespace Framework.Blocks.Tests
{
    public class BlockPositionsTests
    {
        [Test]
        public void Position_CountsAllEvents_NotOnlyBlocks()
        {
            var stream = new EventStream();
            _ = new BlockPositions(stream);
            var block = new TestBlock();

            stream.Emit(new PlainEvent());
            stream.Emit(block);

            Assert.AreEqual(2, block.Position);
        }

        [Test]
        public void ListenerSubscribedBeforePositions_ReadsSetPosition()
        {
            var stream = new EventStream();
            var seen = 0;
            stream.OnAppended += ev => seen = ((Block)ev).Position;
            _ = new BlockPositions(stream);

            stream.Emit(new TestBlock());

            Assert.AreEqual(1, seen);
        }

        [Test]
        public void Positions_MatchOnBothSides()
        {
            var w = WireConnection.Loopback();
            w.ServerNetwork.Register<TestBlock, TestBlock.Packed>();
            w.ClientNetwork.Register<TestBlock, TestBlock.Packed>();
            _ = new BlockPositions(w.ServerStream);
            _ = new BlockPositions(w.ClientStream);

            for (var i = 0; i < 3; i++) w.ServerStream.Emit(new TestBlock { Value = i });

            Assert.AreEqual(3, w.ClientStream.Count);
            for (var i = 0; i < 3; i++)
            {
                Assert.AreEqual(i + 1, ((Block)w.ServerStream[i]).Position);
                Assert.AreEqual(i + 1, ((Block)w.ClientStream[i]).Position);
                Assert.AreEqual(i, ((TestBlock)w.ClientStream[i]).Value);
            }
        }
    }
}
