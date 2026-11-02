using Framework.RecordIds;
using NUnit.Framework;

namespace Framework.Blocks.Tests
{
    public class SourcedTests
    {
        static IEntity SourceOf(object message) => message is ISourced s ? s.Source : null;

        [Test]
        public void SourcedMessage_WritesSourceId()
        {
            var wire = new GroupWire();

            wire.Groups.Create(() => { }, SourceOf(new TestMessage(new TestEntity(4))));

            Assert.AreEqual(new EntityId { Value = 4 }, ((GroupStart)wire.ClientBlocks()[0]).SourceId);
        }

        [Test]
        public void PlainMessage_WritesNullSource()
        {
            var wire = new GroupWire();

            wire.Groups.Create(() => { }, SourceOf(new object()));

            Assert.IsNull(((GroupStart)wire.ClientBlocks()[0]).SourceId);
        }
    }
}
