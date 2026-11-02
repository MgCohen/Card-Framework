using System;
using Framework.Routing;
using NUnit.Framework;

namespace Framework.Pipeline.Tests
{
    public class RouterTests
    {
        [Test]
        public void Router_Single_FindsPingHandler()
        {
            var ping = new PingHandler();
            var router = new TypeRouter<Handler<Message, TestScope>>(new Handler<Message, TestScope>[] { ping }, RouteMode.Single);

            Assert.That(router.Get(typeof(Ping)), Is.SameAs(ping));
        }

        [Test]
        public void Router_Single_FindsPingValidator()
        {
            var ping = new PingValidator();
            var router = new TypeRouter<Validator<Message>>(new Validator<Message>[] { ping }, RouteMode.Single);

            Assert.That(router.Get(typeof(Ping)), Is.SameAs(ping));
        }

        [Test]
        public void Router_Single_DuplicateHandler_Throws()
            => Assert.Throws<InvalidOperationException>(() => new TypeRouter<Handler<Message, TestScope>>(
                new Handler<Message, TestScope>[] { new PingHandler(), new PingHandler() }, RouteMode.Single));

        [Test]
        public void Handle_ThroughInternalAccess_ReturnsPong()
        {
            Handler<Message, TestScope> handler = new PingHandler();

            Assert.That(handler.Handle(new Ping(), new TestScope()).Result, Is.EqualTo("pong"));
        }

        [Test]
        public void Validate_Denied_ReturnsFalseWithReason()
        {
            Validator<Message> validator = new DeniedValidator();

            Assert.IsFalse(validator.Validate(new Denied(), out var reason));
            Assert.That(reason, Is.EqualTo("no mana"));
        }

        [Test]
        public void Validate_Ping_ReturnsTrueWithoutReason()
        {
            Validator<Message> validator = new PingValidator();

            Assert.IsTrue(validator.Validate(new Ping(), out var reason));
            Assert.IsNull(reason);
        }
    }
}
