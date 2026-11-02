using System;
using Framework.RecordIds;
using Framework.TypeRouting;
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
            builder.AddPipeline<TestPipeline, Message, TestScope, TestView>(o => o.IncludeTests = true);
            var pipeline = builder.Build().Resolve<TestPipeline>();

            var e = pipeline.Add(new Ping());
            pipeline.Run().Wait();

            Assert.That(Result<string>.From(e).Value, Is.EqualTo("pong"));
        }

        [Test]
        public void AddPipeline_Default_SkipsTestAssemblies()
        {
            var builder = new ContainerBuilder();
            builder.AddPipeline<TestPipeline, Message, TestScope, TestView>();
            var router = builder.Build().Resolve<TypeRouter<Handler<Message, TestScope>>>();

            Assert.That(router.RoutedTypes, Is.Empty);
        }

        [Test]
        public void AddPipeline_IncludeTests_ScansFixtures()
        {
            var builder = new ContainerBuilder();
            builder.AddPipeline<TestPipeline, Message, TestScope, TestView>(o => o.IncludeTests = true);
            var resolver = builder.Build();

            Assert.That(resolver.Resolve<TypeRouter<Handler<Message, TestScope>>>().Get(typeof(Ping)), Is.TypeOf<PingHandler>());
            Assert.That(resolver.Resolve<TypeRouter<Validator<Message, TestView>>>().Get(typeof(Denied)), Is.TypeOf<DeniedValidator>());
        }

        [Test]
        public void AddPipeline_SameCallTwice_RegistersOnce()
        {
            var builder = new ContainerBuilder();
            builder.AddPipeline<TestPipeline, Message, TestScope, TestView>(o => o.IncludeTests = true);
            builder.AddPipeline<TestPipeline, Message, TestScope, TestView>(o => o.IncludeTests = true);
            var resolver = builder.Build();

            Assert.That(resolver.Resolve<TestPipeline>(), Is.SameAs(resolver.Resolve<TestPipeline>()));
            Assert.That(resolver.Resolve<TypeRouter<Handler<Message, TestScope>>>().Get(typeof(Ping)), Is.TypeOf<PingHandler>());
        }

        [Test]
        public void AddPipeline_RegistersPipelineServices()
        {
            var builder = new ContainerBuilder();
            builder.AddPipeline<TestPipeline, Message, TestScope, TestView>(o => o.IncludeTests = true);
            builder.AddPipeline<OtherPipeline, Message, TestScope, TestView>(o => o.IncludeTests = true);
            var resolver = builder.Build();

            var services = resolver.Resolve<PipelineServices<Message, TestScope, TestView>>();
            Assert.That(services.Handlers, Is.SameAs(resolver.Resolve<TypeRouter<Handler<Message, TestScope>>>()));
            Assert.That(services.Validators, Is.SameAs(resolver.Resolve<TypeRouter<Validator<Message, TestView>>>()));
            Assert.That(services.Ids, Is.SameAs(resolver.Resolve<IdAllocator<MessageId>>()));
        }

        [Test]
        public void AddPipeline_TwoPipelines_ShareOneIdAllocator()
        {
            var builder = new ContainerBuilder();
            builder.AddPipeline<TestPipeline, Message, TestScope, TestView>();
            builder.AddPipeline<OtherPipeline, Message, TestScope, TestView>();
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
            builder.AddPipeline<TestPipeline, Message, TestScope, TestView>(o => o.FirstMessageId = 100);

            var e = builder.Build().Resolve<TestPipeline>().Add(new Ping());

            Assert.That(e.Id.Value, Is.EqualTo(100));
        }

        [Test]
        public void AddPipeline_ChildScope_UsesParentIdSpace()
        {
            var builder = new ContainerBuilder();
            builder.AddPipeline<TestPipeline, Message, TestScope, TestView>();
            var root = builder.Build();
            var child = root.CreateScope(b => b.AddPipeline<OtherPipeline, Message, TestScope, TestView>());

            var a = root.Resolve<TestPipeline>().Add(new Ping());
            var b = child.Resolve<OtherPipeline>().Add(new Ping());

            Assert.That(b.Id.Value, Is.EqualTo(a.Id.Value + 1));
        }

        [Test]
        public void AddPipeline_DifferentIncludeTests_Throws()
        {
            var builder = new ContainerBuilder();
            builder.AddPipeline<TestPipeline, Message, TestScope, TestView>(o => o.IncludeTests = true);

            var ex = Assert.Throws<InvalidOperationException>(() =>
                builder.AddPipeline<OtherPipeline, Message, TestScope, TestView>());
            Assert.That(ex.Message, Does.Contain("IncludeTests"));
        }

        [Test]
        public void AddPipeline_ChildScope_DifferentIncludeTests_Throws()
        {
            var builder = new ContainerBuilder();
            builder.AddPipeline<TestPipeline, Message, TestScope, TestView>(o => o.IncludeTests = true);
            var root = builder.Build();

            Assert.Throws<InvalidOperationException>(() =>
                root.CreateScope(b => b.AddPipeline<OtherPipeline, Message, TestScope, TestView>()));
        }

        [Test]
        public void AddPipeline_DifferentFirstMessageId_Throws()
        {
            var builder = new ContainerBuilder();
            builder.AddPipeline<TestPipeline, Message, TestScope, TestView>();

            Assert.Throws<InvalidOperationException>(() =>
                builder.AddPipeline<OtherPipeline, Message, TestScope, TestView>(o => o.FirstMessageId = 100));
        }

        [Test]
        public void AddPipeline_ClientRole_Throws()
        {
            var builder = new ContainerBuilder();

            var ex = Assert.Throws<InvalidOperationException>(() =>
                builder.AddPipeline<TestPipeline, Message, TestScope, TestView>(o => o.Role = Role.Client));
            Assert.That(ex.Message, Does.Contain("runs only on the server"));
        }
    }
}
