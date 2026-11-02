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

        [TestCase(Role.Server)]
        [TestCase(Role.Client)]
        public void FirstBelowOne_FailsAtInstall(Role role)
        {
            var builder = new ContainerBuilder();

            Assert.Throws<ArgumentOutOfRangeException>(() => builder.AddIdSpace<AId>(role, first: 0));
        }

        [Test]
        public void TwoCalls_WithDifferentFirst_Fail()
        {
            var builder = new ContainerBuilder();
            builder.AddIdSpace<AId>(Role.Server, first: 1);

            var error = Assert.Throws<InvalidOperationException>(() => builder.AddIdSpace<AId>(Role.Server, first: 5));

            StringAssert.Contains("AId", error.Message);
            StringAssert.Contains("1", error.Message);
            StringAssert.Contains("5", error.Message);
        }

        [Test]
        public void ChildScope_ForTheSameSpace_UsesTheParentAllocator()
        {
            var builder = new ContainerBuilder();
            builder.AddIdSpace<AId>(Role.Server);
            var root = builder.Build();

            using var child = root.CreateScope(scope => scope.AddIdSpace<AId>(Role.Server));

            Assert.AreSame(root.Resolve<IdAllocator<AId>>(), child.Resolve<IdAllocator<AId>>());
            Assert.AreEqual(1, child.Resolve<IReadOnlyList<IdAllocator<AId>>>().Count);
        }

        [Test]
        public void ChildScope_WithDifferentFirst_Fails()
        {
            var builder = new ContainerBuilder();
            builder.AddIdSpace<AId>(Role.Server);
            var root = builder.Build();

            Assert.Throws<InvalidOperationException>(() => root.CreateScope(scope => scope.AddIdSpace<AId>(Role.Server, first: 5)));
        }
    }
}
