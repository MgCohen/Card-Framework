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
        public IEnumerator StartHostDirect_EntersInLobbyAsHostAtSeatZero() => UniTask.ToCoroutine(async () =>
        {
            using var session = await Session.StartHostDirect(Port, ListenAddress);

            Assert.AreEqual(SessionState.InLobby, session.State);
            Assert.AreEqual(0, session.Self.Seat);
            Assert.IsTrue(session.IsHost);
            Assert.IsTrue(session.Self.IsLocal);
            Assert.AreEqual(1, session.Peers.Count);
            Assert.NotNull(session.NetworkManager);
        });

        [UnityTest]
        public IEnumerator StartHostDirect_SetSelfData_RoundTripsThroughNgoLoopback() => UniTask.ToCoroutine(async () =>
        {
            using var session = await Session.StartHostDirect(Port, ListenAddress);

            PlayerContext received = null;
            session.PlayerContextChanged += (peer, ctx) => received = ctx;

            session.SetSelfData(new PlayerData { PlayerId = 777 });

            // NGO's CustomMessagingManager delivers on the player loop; the resolver path is
            // submit → host hears self-echo → resolver → broadcast → host hears that too.
            // Give it a few frames.
            for (var i = 0; i < 5; i++) await UniTask.NextFrame();

            Assert.IsNotNull(received, "Host should hear its own PlayerContext broadcast through NGO loopback");
            Assert.AreEqual(0, received.Seat);
            Assert.AreEqual(777UL, received.PlayerId);
        });

        [UnityTest]
        public IEnumerator StartHostDirect_StartMatch_TransitionsToInMatch() => UniTask.ToCoroutine(async () =>
        {
            using var session = await Session.StartHostDirect(Port, ListenAddress);

            session.StartMatch();
            for (var i = 0; i < 3; i++) await UniTask.NextFrame();

            Assert.AreEqual(SessionState.InMatch, session.State);
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
