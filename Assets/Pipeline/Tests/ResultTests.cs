using System;
using NUnit.Framework;

namespace Framework.Pipeline.Tests
{
    public class ResultTests
    {
        static Envelope<Ping> Listed() => new Envelope<Ping>(new MessageId { Value = 1 }, new Ping());

        static Envelope<Ping> Rejected(string reason)
        {
            var e = Listed();
            e.Reject(reason);
            return e;
        }

        static Envelope<Ping> Accepted(object result)
        {
            var e = Listed();
            e.Accept(result);
            return e;
        }

        [Test]
        public void From_Rejected_NotAccepted()
        {
            var result = Result.From(Rejected("no mana"));

            Assert.IsFalse(result.Accepted);
            Assert.That(result.Reason, Is.EqualTo("no mana"));
        }

        [Test]
        public void From_Accepted_IsAccepted()
        {
            var result = Result.From(Accepted(null));

            Assert.IsTrue(result.Accepted);
            Assert.IsNull(result.Reason);
        }

        [Test]
        public void FromT_Accepted_ReturnsValue()
            => Assert.That(Result<int>.From(Accepted(5)).Value, Is.EqualTo(5));

        [Test]
        public void FromT_Rejected_ReturnsDefault()
            => Assert.That(Result<int>.From(Rejected("no mana")).Value, Is.EqualTo(0));

        [Test]
        public void From_Listed_Throws()
            => Assert.Throws<InvalidOperationException>(() => Result.From(Listed()));

        [Test]
        public void From_Running_Throws()
        {
            var e = Listed();
            e.Start();

            Assert.Throws<InvalidOperationException>(() => Result.From(e));
        }

        [Test]
        public void From_Faulted_Throws()
        {
            var e = Listed();
            e.Fault();

            Assert.Throws<InvalidOperationException>(() => Result.From(e));
        }

        [Test]
        public void FromT_Listed_Throws()
            => Assert.Throws<InvalidOperationException>(() => Result<int>.From(Listed()));

        [Test]
        public void FromT_Faulted_Throws()
        {
            var e = Listed();
            e.Fault();

            Assert.Throws<InvalidOperationException>(() => Result<int>.From(e));
        }

        [Test]
        public void FromT_WrongValueType_Throws()
            => Assert.Throws<InvalidOperationException>(() => Result<int>.From(Accepted("five")));

        [Test]
        public void FromT_NullValueForReferenceType_ReturnsNull()
            => Assert.IsNull(Result<string>.From(Accepted(null)).Value);

        [Test]
        public void Envelope_Message_ReturnsTypedAndBaseMessage()
        {
            var e = Listed();
            Envelope untyped = e;

            Assert.That(untyped.Message, Is.SameAs(e.Message));
            Assert.That(e.State, Is.EqualTo(EnvelopeState.Listed));
            Assert.IsNull(e.ParentId);
        }
    }
}
