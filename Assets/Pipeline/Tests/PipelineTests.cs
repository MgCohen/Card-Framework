using System;
using NUnit.Framework;

namespace Framework.Pipeline.Tests
{
    public class PipelineTests
    {
        [Test]
        public void Add_GivesEachEnvelopeTheNextId()
        {
            var pipeline = new TestPipeline(new PingHandler());

            var a = pipeline.Add(new Ping());
            var b = pipeline.Add(new Ping());

            Assert.That(a.Id.Value, Is.EqualTo(1));
            Assert.That(b.Id.Value, Is.EqualTo(2));
            Assert.That(pipeline.Items, Is.EqualTo(new[] { a, b }));
        }

        [Test]
        public void Steps_RunInFixedOrder()
        {
            var pipeline = new TestPipeline(new PingHandler());
            var e = pipeline.Add(new Ping());

            pipeline.Run().Wait();

            Assert.That(pipeline.Steps, Is.EqualTo(new[] { "validate", "scope", "before", "handle", "after" }));
            Assert.That(e.State, Is.EqualTo(EnvelopeState.Accepted));
            Assert.That(e.Result, Is.EqualTo("pong"));
        }

        [Test]
        public void Reject_RunsNoOtherStep()
        {
            var pipeline = new TestPipeline(new DeniedValidator());
            var e = pipeline.Add(new Denied());

            pipeline.Run().Wait();

            Assert.That(pipeline.Steps, Is.EqualTo(new[] { "validate" }));
            Assert.That(e.State, Is.EqualTo(EnvelopeState.Rejected));
            Assert.That(e.Reason, Is.EqualTo("no mana"));
        }

        [Test]
        public void Fault_ClosesScope()
        {
            var pipeline = new TestPipeline(new BoomHandler());
            var e = pipeline.Add(new Boom());

            var run = pipeline.Run();

            Assert.IsTrue(run.IsFaulted);
            Assert.That(run.Exception.InnerException.Message, Is.EqualTo("boom"));
            Assert.That(e.State, Is.EqualTo(EnvelopeState.Faulted));
            Assert.IsTrue(pipeline.Scopes[0].IsClosed);
            Assert.That(pipeline.Steps, Has.No.Member("after"));
        }

        [Test]
        public void SelectNext_Fifo()
        {
            var pipeline = new TestPipeline(new PingHandler());
            var first = pipeline.Add(new Ping());
            var second = pipeline.Add(new Ping());

            pipeline.Run().Wait();

            Assert.That(first.State, Is.EqualTo(EnvelopeState.Accepted));
            Assert.That(second.State, Is.EqualTo(EnvelopeState.Listed));
            Assert.That(pipeline.Items, Is.EqualTo(new[] { second }));
        }

        [Test]
        public void SelectNext_LifoOverride()
        {
            var pipeline = new TestPipeline(new PingHandler()) { Lifo = true };
            var first = pipeline.Add(new Ping());
            var second = pipeline.Add(new Ping());

            Assert.IsTrue(pipeline.TryPeek(out var next));
            Assert.That(next, Is.SameAs(second));

            pipeline.Run().Wait();

            Assert.That(second.State, Is.EqualTo(EnvelopeState.Accepted));
            Assert.That(first.State, Is.EqualTo(EnvelopeState.Listed));
        }

        [Test]
        public void Run_EmptyList_DoesNothing()
        {
            var pipeline = new TestPipeline(new PingHandler());

            Assert.IsTrue(pipeline.Run().IsCompleted);
            Assert.IsFalse(pipeline.TryPeek(out _));
            Assert.That(pipeline.Steps, Is.Empty);
        }

        [Test]
        public void TryTake_RemovesWithoutRun()
        {
            var pipeline = new TestPipeline(new PingHandler());
            var e = pipeline.Add(new Ping());

            Assert.IsTrue(pipeline.TryTake(out var taken));

            Assert.That(taken, Is.SameAs(e));
            Assert.That(e.State, Is.EqualTo(EnvelopeState.Listed));
            Assert.That(pipeline.Items, Is.Empty);
            Assert.IsFalse(pipeline.TryTake(out _));
        }

        [Test]
        public void Remove_Listed_ReturnsTrue()
        {
            var pipeline = new TestPipeline(new PingHandler());
            var e = pipeline.Add(new Ping());

            Assert.IsTrue(pipeline.Remove(e.Id));
            Assert.That(pipeline.Items, Is.Empty);
        }

        [Test]
        public void Remove_Running_ReturnsFalse()
        {
            var hold = new HoldHandler();
            var pipeline = new TestPipeline(hold);
            var e = pipeline.Add(new Hold());

            var run = pipeline.Run();

            Assert.That(e.State, Is.EqualTo(EnvelopeState.Running));
            Assert.IsFalse(pipeline.Remove(e.Id));
            Assert.IsFalse(run.IsCompleted);
            hold.Release.SetResult(null);
        }

        [Test]
        public void RunInline_SetsParentId()
        {
            var pipeline = new TestPipeline(new NestHandler(), new PingHandler());
            var parent = pipeline.Add(new Nest());

            pipeline.Run().Wait();

            var child = (Envelope<Message>)parent.Result;
            Assert.That(child.ParentId, Is.EqualTo(parent.Id));
            Assert.That(child.Id.Value, Is.EqualTo(2));
            Assert.That(child.State, Is.EqualTo(EnvelopeState.Accepted));
            Assert.That(child.Result, Is.EqualTo("pong"));
            Assert.That(pipeline.Items, Is.Empty);
        }

        [Test]
        public void Handle_TypedResult_ReachesResultT()
        {
            var pipeline = new TestPipeline(new EchoHandler());
            var e = pipeline.Add(new Echo { Value = 5 });

            pipeline.Run().Wait();

            Assert.That(Result<int>.From(e).Value, Is.EqualTo(5));
        }

        [Test]
        public void ScopeVerb_AfterRun_Throws()
        {
            var pipeline = new TestPipeline(new PingHandler());
            pipeline.Add(new Ping());

            pipeline.Run().Wait();

            Assert.Throws<InvalidOperationException>(() => pipeline.Scopes[0].Run(new Ping()));
        }

        [Test]
        public void Handle_NoHandler_Faults()
        {
            var pipeline = new TestPipeline();
            var e = pipeline.Add(new Ping());

            Assert.IsTrue(pipeline.Run().IsFaulted);
            Assert.That(e.State, Is.EqualTo(EnvelopeState.Faulted));
        }
    }
}
