using System.Collections;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Framework.Multiplayer.PlayTests
{
    public class SoloSessionTests
    {
        [UnityTest]
        public IEnumerator Solo_StartsConnectedAsHostAtSeatZero() => UniTask.ToCoroutine(async () =>
        {
            using var session = await Session.StartSolo();

            Assert.AreEqual(SessionState.Connected, session.State);
            Assert.AreEqual(0, session.Self.Seat);
            Assert.IsTrue(session.IsHost);
            Assert.IsTrue(session.Self.IsLocal);
            Assert.AreEqual(1, session.Peers.Count);
            Assert.AreEqual(session.Self.Seat, session.Peers[0].Seat);
        });

        [UnityTest]
        public IEnumerator Solo_UserChannel_SelfEchoesThroughLoopback() => UniTask.ToCoroutine(async () =>
        {
            using var session = await Session.StartSolo();
            var channel = session.Network.Register<ProbeEvent>();

            ProbeEvent received = null;
            channel.OnReceived += ev => received = ev;

            session.Network.Send(new ProbeEvent { Value = 42 });

            Assert.IsNotNull(received, "Solo loopback should echo sends back to the same network");
            Assert.AreEqual(42, received.Value);
        });

        [UnityTest]
        public IEnumerator Solo_StreamAutoAppendsReceivedEvents() => UniTask.ToCoroutine(async () =>
        {
            using var session = await Session.StartSolo();
            session.Network.Register<ProbeEvent>();

            Assert.AreEqual(0, session.Stream.Count);

            session.Network.Send(new ProbeEvent { Value = 7 });
            session.Network.Send(new ProbeEvent { Value = 8 });

            Assert.AreEqual(2, session.Stream.Count);
            Assert.IsInstanceOf<ProbeEvent>(session.Stream[0]);
            Assert.AreEqual(7, ((ProbeEvent)session.Stream[0]).Value);
            Assert.AreEqual(8, ((ProbeEvent)session.Stream[1]).Value);
        });

        [UnityTest]
        public IEnumerator Solo_DisposeFlipsToDisconnectedAndRaisesOnce() => UniTask.ToCoroutine(async () =>
        {
            var session = await Session.StartSolo();

            var raised = 0;
            DisconnectReason reason = default;
            session.Disconnected += r => { raised++; reason = r; };

            session.Dispose();
            session.Dispose();  // idempotent

            Assert.AreEqual(SessionState.Disconnected, session.State);
            Assert.AreEqual(1, raised);
            Assert.AreEqual(DisconnectReason.LocalShutdown, reason);
        });
    }
}
