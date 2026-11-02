using System;
using System.Collections.Generic;
using Framework.RecordIds;
using Framework.Wire;
using NUnit.Framework;
using VContainer;

namespace Framework.Blocks.Tests
{
    public class BlocksInstallTests
    {
        static ContainerBuilder Builder()
        {
            var builder = new ContainerBuilder();
            builder.Register<EventStream>(Lifetime.Singleton).AsSelf().As<IEventEmitter>();
            return builder;
        }

        [Test]
        public void AddBlocks_Only_ResolvesGroupServiceAndBlockPositions()
        {
            var builder = Builder();
            builder.AddBlocks();

            var container = builder.Build();

            Assert.IsNotNull(container.Resolve<BlockPositions>());
            Assert.AreEqual(new BlockId { Value = 1 }, container.Resolve<GroupService>().Open());
        }

        [Test]
        public void ClientRole_SetsPositions_HasNoGroupService()
        {
            var builder = Builder();
            builder.AddBlocks(o => o.Role = Role.Client);

            var container = builder.Build();
            var stream = container.Resolve<EventStream>();
            stream.Emit(new TestBlock());

            Assert.AreEqual(1, ((Block)stream[0]).Position);
            Assert.Throws<VContainerException>(() => container.Resolve<GroupService>());
        }

        [Test]
        public void TwoCalls_Merge()
        {
            var builder = Builder();
            builder.AddBlocks();
            builder.AddBlocks();

            var container = builder.Build();

            Assert.AreEqual(1, container.Resolve<IReadOnlyList<BlockPositions>>().Count);
            Assert.AreEqual(1, container.Resolve<IReadOnlyList<GroupService>>().Count);
        }

        [Test]
        public void TwoCalls_WithDifferentRole_Fail()
        {
            var builder = Builder();
            builder.AddBlocks(o => o.Role = Role.Server);

            var error = Assert.Throws<InvalidOperationException>(() => builder.AddBlocks(o => o.Role = Role.Client));

            StringAssert.Contains("Server", error.Message);
        }

        [Test]
        public void ChildScope_SameRole_UsesTheParentInstall()
        {
            var builder = Builder();
            builder.AddBlocks();
            var parent = builder.Build();

            using var child = parent.CreateScope(b => b.AddBlocks());

            Assert.AreSame(parent.Resolve<BlockPositions>(), child.Resolve<BlockPositions>());
        }

        [Test]
        public void ChildScope_DifferentRole_Fails()
        {
            var builder = Builder();
            builder.AddBlocks();
            var parent = builder.Build();

            Assert.Throws<InvalidOperationException>(() => parent.CreateScope(b => b.AddBlocks(o => o.Role = Role.Client)));
        }
    }
}
