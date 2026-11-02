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
        public void AddBlocks_Default_ResolvesGroupServiceAndBlockPositions()
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
        public void TwoCalls_WithDifferentIncludeTests_Fail()
        {
            var builder = Builder();
            builder.AddBlocks();

            var error = Assert.Throws<InvalidOperationException>(() => builder.AddBlocks(o => o.IncludeTests = true));

            StringAssert.Contains("IncludeTests", error.Message);
        }

        [Test]
        public void WithNetwork_RegistersEachBlockType_OnBothEnds()
        {
            var wire = Wire.Wire.Connect();
            var server = new ContainerBuilder();
            server.RegisterInstance(wire.ServerStream).AsSelf().As<IEventEmitter>();
            server.RegisterInstance(wire.ServerNetwork);
            server.AddBlocks(o => o.IncludeTests = true);
            var client = new ContainerBuilder();
            client.RegisterInstance(wire.ClientStream);
            client.RegisterInstance(wire.ClientNetwork);
            client.AddBlocks(o => { o.Role = Role.Client; o.IncludeTests = true; });
            var groups = server.Build().Resolve<GroupService>();
            client.Build();

            groups.Create(() => wire.ServerStream.Emit(new TestBlock { Value = 9 }));

            Assert.AreEqual(3, wire.ClientStream.Count);
            Assert.IsInstanceOf<GroupStart>(wire.ClientStream[0]);
            Assert.AreEqual(9, ((TestBlock)wire.ClientStream[1]).Value);
            Assert.AreEqual(2, ((Block)wire.ClientStream[1]).Position);
            Assert.IsInstanceOf<GroupEnd>(wire.ClientStream[2]);
        }

        [Test]
        public void ListenerBeforeAddBlocks_ReadsZero_ListenerAfter_ReadsPosition()
        {
            var builder = Builder();
            var before = -1;
            var after = -1;
            builder.RegisterBuildCallback(c => c.Resolve<EventStream>().OnAppended += ev => before = ((Block)ev).Position);
            builder.AddBlocks(o => o.Role = Role.Client);
            builder.RegisterBuildCallback(c => c.Resolve<EventStream>().OnAppended += ev => after = ((Block)ev).Position);

            builder.Build().Resolve<EventStream>().Emit(new TestBlock());

            Assert.AreEqual(0, before);
            Assert.AreEqual(1, after);
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
