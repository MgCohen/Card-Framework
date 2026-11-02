using Framework.Stripping;
using NUnit.Framework;

namespace Framework.Wire.Tests
{
    // A unit test cannot run the linker. These tests show the two parts that keep ids equal:
    // WireEvent carries the attribute that the linker reads, and the registrar gives each end the same ids.
    public class WireRegistrarTests
    {
        [Test]
        public void WireEvent_HasRequireDerived()
        {
            Assert.That(typeof(WireEvent).IsDefined(typeof(RequireDerivedAttribute), false), Is.True);
        }

        [Test]
        public void RegisterAll_TypeThatNoEndConstructs_GivesEqualIdsOnBothEnds()
        {
            var serverTransport = new CaptureTransport();
            var clientTransport = new CaptureTransport();
            var server = new Network(serverTransport);
            var client = new Network(clientTransport);

            WireRegistrar.RegisterAll(server);
            WireRegistrar.RegisterAll(client);
            server.Send(new PingEvent());
            client.Send(new PingEvent());

            Assert.IsTrue(server.IsRegistered(typeof(AUnconstructedEvent)));
            Assert.IsTrue(client.IsRegistered(typeof(AUnconstructedEvent)));
            Assert.AreEqual(serverTransport.Sent[0][0], clientTransport.Sent[0][0]);
        }
    }
}
