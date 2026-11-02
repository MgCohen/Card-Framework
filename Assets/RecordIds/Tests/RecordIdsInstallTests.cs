using System;
using System.Collections.Generic;
using NUnit.Framework;
using VContainer;

namespace Framework.RecordIds.Tests
{
    public class RecordIdsInstallTests
    {
        [Test]
        public void AddIdSpace_Only_ResolvesTheAllocator()
        {
            var builder = new ContainerBuilder();
            builder.AddIdSpace<AId>(Role.Server);

            Assert.AreEqual(new AId { Value = 1 }, builder.Build().Resolve<IdAllocator<AId>>().Next());
        }

        [Test]
        public void TwoCalls_ForTheSameSpace_GiveOneAllocator()
        {
            var builder = new ContainerBuilder();
            builder.AddIdSpace<AId>(Role.Server);
            builder.AddIdSpace<AId>(Role.Server);

            var container = builder.Build();

            Assert.AreEqual(1, container.Resolve<IReadOnlyList<IdAllocator<AId>>>().Count);
            Assert.AreSame(container.Resolve<IdAllocator<AId>>(), container.Resolve<IdAllocator<AId>>());
        }

        [Test]
        public void TwoSpaces_GiveTwoAllocators()
        {
            var builder = new ContainerBuilder();
            builder.AddIdSpace<AId>(Role.Server);
            builder.AddIdSpace<BId>(Role.Server);

            var container = builder.Build();

            Assert.AreEqual(1, container.Resolve<IdAllocator<AId>>().Next().Value);
            Assert.AreEqual(1, container.Resolve<IdAllocator<BId>>().Next().Value);
        }

        [Test]
        public void ClientRole_RegistersNoAllocator()
        {
            var builder = new ContainerBuilder();
            builder.AddIdSpace<AId>(Role.Client);

            Assert.Throws<VContainerException>(() => builder.Build().Resolve<IdAllocator<AId>>());
        }

        [Test]
        public void First_SetsTheFirstId()
        {
            var builder = new ContainerBuilder();
            builder.AddIdSpace<AId>(Role.Server, first: 100);

            Assert.AreEqual(100, builder.Build().Resolve<IdAllocator<AId>>().Next().Value);
        }

        [Test]
        public void FirstBelowOne_FailsAtInstall()
        {
            var builder = new ContainerBuilder();

            Assert.Throws<ArgumentOutOfRangeException>(() => builder.AddIdSpace<AId>(Role.Server, first: 0));
        }
    }
}
