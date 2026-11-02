using System;
using NUnit.Framework;

namespace Framework.Wire.Tests
{
    public class NetworkRegistrationTests
    {
        [Test]
        public void TryRegister_New_ReturnsTrue_AndIsRegistered()
        {
            var network = new Network(new NullTransport());

            Assert.IsTrue(network.TryRegister(typeof(PingEvent)));
            Assert.IsTrue(network.IsRegistered(typeof(PingEvent)));
        }

        [Test]
        public void TryRegister_Twice_ReturnsFalse_AndKeepsOneChannel()
        {
            var capture = new CaptureTransport();
            var network = new Network(capture);
            network.TryRegister(typeof(PingEvent));

            Assert.IsFalse(network.TryRegister(typeof(PingEvent)));
            network.Send(new PingEvent());
            Assert.AreEqual(0, capture.Sent[0][0]);
        }

        [Test]
        public void TryRegister_AfterGenericRegister_ReturnsFalse()
        {
            var network = new Network(new NullTransport());
            network.Register<PingEvent, PingEvent.Packed>();

            Assert.IsFalse(network.TryRegister(typeof(PingEvent)));
        }

        [Test]
        public void Register_Twice_Throws()
        {
            var network = new Network(new NullTransport());
            network.Register<PingEvent, PingEvent.Packed>();

            Assert.Throws<InvalidOperationException>(() => network.Register<PingEvent, PingEvent.Packed>());
        }

        [Test]
        public void TryRegister_ThenRegister_Throws()
        {
            var network = new Network(new NullTransport());
            network.TryRegister(typeof(PingEvent));

            Assert.Throws<InvalidOperationException>(() => network.Register<PingEvent, PingEvent.Packed>());
        }

        [Test]
        public void TryRegister_RoundTrips_OverLoopback()
        {
            var w = WireConnection.Loopback();
            w.ServerNetwork.TryRegister(typeof(PingEvent));
            w.ClientNetwork.TryRegister(typeof(PingEvent));

            w.ServerStream.Emit(new PingEvent { Value = 5 });

            Assert.AreEqual(5, ((PingEvent)w.ClientStream[0]).Value);
        }

        [Test]
        public void TryRegister_NotAWireEvent_Throws()
        {
            var network = new Network(new NullTransport());

            Assert.Throws<ArgumentException>(() => network.TryRegister(typeof(string)));
            Assert.Throws<ArgumentException>(() => network.TryRegister(typeof(WireEvent)));
        }
    }
}
