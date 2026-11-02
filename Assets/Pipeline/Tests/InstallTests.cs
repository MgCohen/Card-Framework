using System;
using Framework.RecordIds;
using Framework.Routing;
using NUnit.Framework;
using VContainer;

namespace Framework.Pipeline.Tests
{
    public class InstallTests
    {
        [Test]
        public void AddPipeline_RunsOneMessage()
        {
            var builder = new ContainerBuilder();
            builder.AddPipeline<TestPipeline, Message, TestScope>();
            // The scan skips test assemblies by default, so the fixture registers by hand.
            builder.Register<PingHandler>(Lifetime.Singleton).As<Handler<Message, TestScope>>();
            var pipeline = builder.Build().Resolve<TestPipeline>();

            var e = pipeline.Add(new Ping());
            pipeline.Run().Wait();

            Assert.That(Result<string>.From(e).Value, Is.EqualTo("pong"));
        }

        [Test]
        public void AddPipeline_Default_SkipsTestAssemblies()
        {
            var builder = new ContainerBuilder();
            builder.AddPipeline<TestPipeline, Message, TestScope>();
            var router = builder.Build().Resolve<TypeRouter<Handler<Message, TestScope>>>();

            Assert.That(router.RoutedTypes, Is.Empty);
        }

        [Test]
        public void AddPipeline_IncludeTests_ScansFixtures()
        {
            var builder = new ContainerBuilder();
            builder.AddPipeline<TestPipeline, Message, TestScope>(o => o.IncludeTests = true);
            var resolver = builder.Build();

            Assert.That(resolver.Resolve<TypeRouter<Handler<Message, TestScope>>>().Get(typeof(Ping)), Is.TypeOf<PingHandler>());
            Assert.That(resolver.Resolve<TypeRouter<Validator<Message>>>().Get(typeof(Denied)), Is.TypeOf<DeniedValidator>());
        }

        [Test]
        public void AddPipeline_SameCallTwice_RegistersOnce()
        {
            var builder = new ContainerBuilder();
            builder.AddPipeline<TestPipeline, Message, TestScope>(o => o.IncludeTests = true);
            builder.AddPipeline<TestPipeline, Message, TestScope>(o => o.IncludeTests = true);
            var resolver = builder.Build();

            Assert.That(resolver.Resolve<TestPipeline>(), Is.SameAs(resolver.Resolve<TestPipeline>()));
            Assert.That(resolver.Resolve<TypeRouter<Handler<Message, TestScope>>>().Get(typeof(Ping)), Is.TypeOf<PingHandler>());
        }

        [Test]
        public void AddPipeline_TwoPipelines_ShareOneIdAllocator()
        {
            var builder = new ContainerBuilder();
            builder.AddPipeline<TestPipeline, Message, TestScope>();
            builder.AddPipeline<OtherPipeline, Message, TestScope>();
            var resolver = builder.Build();

            var a = resolver.Resolve<TestPipeline>().Add(new Ping());
            var b = resolver.Resolve<OtherPipeline>().Add(new Ping());

            Assert.That(a.Id.Value, Is.EqualTo(1));
            Assert.That(b.Id.Value, Is.EqualTo(2));
        }

        [Test]
        public void AddPipeline_FirstMessageId_StartsTheSpace()
        {
            var builder = new ContainerBuilder();
            builder.AddPipeline<TestPipeline, Message, TestScope>(o => o.FirstMessageId = 100);

            var e = builder.Build().Resolve<TestPipeline>().Add(new Ping());

            Assert.That(e.Id.Value, Is.EqualTo(100));
        }

        [Test]
        public void AddPipeline_ChildScope_UsesParentIdSpace()
        {
            var builder = new ContainerBuilder();
            builder.AddPipeline<TestPipeline, Message, TestScope>();
            var root = builder.Build();
            var child = root.CreateScope(b => b.AddPipeline<OtherPipeline, Message, TestScope>());

            var a = root.Resolve<TestPipeline>().Add(new Ping());
            var b = child.Resolve<OtherPipeline>().Add(new Ping());

            Assert.That(b.Id.Value, Is.EqualTo(a.Id.Value + 1));
        }

        [Test]
        public void AddPipeline_ClientRole_Throws()
        {
            var builder = new ContainerBuilder();

            var ex = Assert.Throws<InvalidOperationException>(() =>
                builder.AddPipeline<TestPipeline, Message, TestScope>(o => o.Role = Role.Client));
            Assert.That(ex.Message, Does.Contain("runs only on the server"));
        }
    }
}
