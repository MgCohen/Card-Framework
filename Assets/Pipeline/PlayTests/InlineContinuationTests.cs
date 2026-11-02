using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Framework.Pipeline.PlayTests
{
    public class InlineContinuationTests
    {
        [UnityTest]
        public IEnumerator RunInline_CompletesInSameFrame()
        {
            var pipeline = new PlayPipeline(new PingHandler());
            var frame = Time.frameCount;

            var task = pipeline.RunInlineForTest(new Ping());

            Assert.IsTrue(task.IsCompleted);
            Assert.That(Time.frameCount, Is.EqualTo(frame));
            Assert.That(task.Result.State, Is.EqualTo(EnvelopeState.Accepted));
            yield break;
        }

        [UnityTest]
        public IEnumerator Run_NestedInline_CompletesInSameFrame()
        {
            var pipeline = new PlayPipeline(new NestHandler(), new PingHandler());
            var parent = pipeline.Add(new Nest());
            var frame = Time.frameCount;

            var run = pipeline.Run();

            Assert.IsTrue(run.IsCompleted);
            Assert.That(Time.frameCount, Is.EqualTo(frame));
            Assert.That(parent.State, Is.EqualTo(EnvelopeState.Accepted));
            var child = (Envelope<Message>)parent.Result;
            Assert.That(child.ParentId, Is.EqualTo(parent.Id));
            Assert.That(child.State, Is.EqualTo(EnvelopeState.Accepted));
            yield break;
        }
    }
}
