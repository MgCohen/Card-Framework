using Framework.RecordIds;
using Framework.Wire;
using NUnit.Framework;

namespace Framework.Blocks.Tests
{
    public class GroupWireTests
    {
        static WireConnection Connect()
        {
            var w = Wire.Wire.Connect();
            w.ServerNetwork.Register<GroupStart, GroupStart.Packed>();
            w.ClientNetwork.Register<GroupStart, GroupStart.Packed>();
            w.ServerNetwork.Register<GroupEnd, GroupEnd.Packed>();
            w.ClientNetwork.Register<GroupEnd, GroupEnd.Packed>();
            return w;
        }

        [Test]
        public void GroupStart_RoundTrips_NullTrigger()
        {
            var w = Connect();

            w.ServerStream.Emit(new GroupStart { TriggerId = null, SourceId = new EntityId { Value = 7 } });

            var start = (GroupStart)w.ClientStream[0];
            Assert.IsNull(start.TriggerId);
            Assert.AreEqual(new EntityId { Value = 7 }, start.SourceId);
        }

        [Test]
        public void GroupStart_RoundTrips_SetTrigger_NullSource()
        {
            var w = Connect();

            w.ServerStream.Emit(new GroupStart { TriggerId = new BlockId { Value = 3 }, SourceId = null });

            var start = (GroupStart)w.ClientStream[0];
            Assert.AreEqual(new BlockId { Value = 3 }, start.TriggerId);
            Assert.IsNull(start.SourceId);
        }

        [Test]
        public void GroupEnd_RoundTrips_StartIdAndOutcome()
        {
            var w = Connect();

            w.ServerStream.Emit(new GroupEnd { StartId = new BlockId { Value = 5 }, Outcome = (byte)GroupOutcome.Errored });

            var end = (GroupEnd)w.ClientStream[0];
            Assert.AreEqual(new BlockId { Value = 5 }, end.StartId);
            Assert.AreEqual(GroupOutcome.Errored, (GroupOutcome)end.Outcome);
        }
    }
}
