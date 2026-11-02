using System.Collections.Generic;
using System.Linq;
using Framework.Wire;

namespace Framework.Blocks.Tests
{
    // A loopback pair with the group blocks registered and BlockPositions on both ends.
    public sealed class GroupWire
    {
        public readonly EventStream Server;
        public readonly EventStream Client;
        public readonly GroupService Groups;

        public GroupWire()
        {
            var w = Wire.Wire.Connect();
            w.ServerNetwork.Register<GroupStart, GroupStart.Packed>();
            w.ClientNetwork.Register<GroupStart, GroupStart.Packed>();
            w.ServerNetwork.Register<GroupEnd, GroupEnd.Packed>();
            w.ClientNetwork.Register<GroupEnd, GroupEnd.Packed>();
            _ = new BlockPositions(w.ServerStream);
            _ = new BlockPositions(w.ClientStream);
            Server = w.ServerStream;
            Client = w.ClientStream;
            Groups = new GroupService(w.ServerStream);
        }

        public List<Block> ClientBlocks()
            => Enumerable.Range(0, Client.Count).Select(i => (Block)Client[i]).ToList();

        public List<GroupOutcome> ClientOutcomes()
            => ClientBlocks().OfType<GroupEnd>().Select(e => (GroupOutcome)e.Outcome).ToList();
    }
}
