using System.Collections;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using Unity.Netcode;
using UnityEngine.TestTools;

namespace Framework.Multiplayer.PlayTests
{
    public class DirectLoopbackTests
    {
        const string ListenAddress = "127.0.0.1";
        const ushort Port = 7788;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            // Belt-and-braces: if a test failed mid-way, scrub any stray NetworkManager singleton
            // so the next test starts clean. Successful tests dispose the Session which destroys
            // its own NM, so this is a no-op in the happy path.
            if (NetworkManager.Singleton != null)
            {
                UnityEngine.Object.DestroyImmediate(NetworkManager.Singleton.gameObject);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator StartHostDirect_EntersConnectedAsHostAtSeatZero() => UniTask.ToCoroutine(async () =>
        {
            using var session = await Session.StartHostDirect(Port, ListenAddress);

            Assert.AreEqual(SessionState.Connected, session.State);
            Assert.AreEqual(0, session.Self.Seat);
            Assert.IsTrue(session.IsHost);
            Assert.IsTrue(session.Self.IsLocal);
            Assert.AreEqual(1, session.Peers.Count);
            Assert.NotNull(session.Network);
            Assert.NotNull(session.Stream);
        });

        [UnityTest]
        public IEnumerator StartHostDirect_UserChannel_SelfEchoesThroughNgo() => UniTask.ToCoroutine(async () =>
        {
            using var session = await Session.StartHostDirect(Port, ListenAddress);
            var channel = session.Network.Register<ProbeEvent>();

            ProbeEvent received = null;
            channel.OnReceived += ev => received = ev;

            session.Network.Send(new ProbeEvent { Value = 99 });

            // NGO's CustomMessagingManager delivers via the player loop — wait a couple of frames.
            await UniTask.NextFrame();
            await UniTask.NextFrame();

            Assert.IsNotNull(received, "Host should hear its own broadcast through NGO loopback");
            Assert.AreEqual(99, received.Value);
        });

        [UnityTest]
        public IEnumerator StartHostDirect_DisposeTearsDownNetworkManagerAndRaisesOnce() => UniTask.ToCoroutine(async () =>
        {
            var session = await Session.StartHostDirect(Port, ListenAddress);

            var raised = 0;
            DisconnectReason reason = default;
            session.Disconnected += r => { raised++; reason = r; };

            session.Dispose();
            session.Dispose();  // idempotent

            // After Dispose the Session should have destroyed the NM GameObject, so the
            // singleton clears synchronously (Destroy is queued, but the static unsets earlier).
            await UniTask.NextFrame();

            Assert.AreEqual(SessionState.Disconnected, session.State);
            Assert.AreEqual(1, raised);
            Assert.AreEqual(DisconnectReason.LocalShutdown, reason);
            Assert.IsNull(NetworkManager.Singleton, "Session.Dispose should destroy the owned NetworkManager");
        });

        [UnityTest]
        public IEnumerator CreateNetworkManager_RefusesWhenSingletonAlreadyExists() => UniTask.ToCoroutine(async () =>
        {
            using var session = await Session.StartHostDirect(Port, ListenAddress);

            Assert.NotNull(NetworkManager.Singleton);
            Assert.Throws<SessionStartupException>(() => Session.CreateNetworkManager());
        });
    }
}
