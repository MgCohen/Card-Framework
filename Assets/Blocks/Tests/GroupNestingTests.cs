using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Framework.Blocks.Tests
{
    public class GroupNestingTests
    {
        [Test]
        public void ClientWalk_EachStartIdIsOnTopOfTheStack()
        {
            var wire = new GroupWire();
            var groups = wire.Groups;

            groups.Create(null, () =>
            {
                groups.Create(null, () => groups.Create(null, () => { }));
                groups.Create(null, () => { });
            });

            var walk = new Stack<BlockId>();
            var maxDepth = 0;
            foreach (var block in wire.ClientBlocks())
            {
                if (block is GroupStart start) walk.Push(start.Id);
                else if (block is GroupEnd end) Assert.AreEqual(walk.Pop(), end.StartId);
                maxDepth = Math.Max(maxDepth, walk.Count);
            }
            Assert.AreEqual(0, walk.Count);
            Assert.AreEqual(3, maxDepth);
        }
    }
}
