using System;
using Framework.TypeRouting;
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
            var router = new TypeRouter<Validator<Message, TestView>>(new Validator<Message, TestView>[] { ping }, RouteMode.Single);

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

            Assert.That(handler.Dispatch(new Ping(), new TestScope()).Result, Is.EqualTo("pong"));
        }

        [Test]
        public void Validate_Denied_ReturnsFalseWithReason()
        {
            Validator<Message, TestView> validator = new DeniedValidator();

            Assert.IsFalse(validator.Dispatch(new Denied(), new TestView(), out var reason));
            Assert.That(reason, Is.EqualTo("no mana"));
        }

        [Test]
        public void Validate_ReadsTheView()
        {
            Validator<Message, TestView> validator = new DeniedValidator();

            Assert.IsTrue(validator.Dispatch(new Denied(), new TestView(mana: 1), out var reason));
            Assert.IsNull(reason);
        }

        [Test]
        public void TypedMembers_RouteByTheirMessageType()
        {
            Assert.That(new PingHandler().RouteType, Is.EqualTo(typeof(Ping)));
            Assert.That(new DeniedValidator().RouteType, Is.EqualTo(typeof(Denied)));
        }

        [Test]
        public void Validate_Ping_ReturnsTrueWithoutReason()
        {
            Validator<Message, TestView> validator = new PingValidator();

            Assert.IsTrue(validator.Dispatch(new Ping(), new TestView(), out var reason));
            Assert.IsNull(reason);
        }
    }
}
