using System.Collections;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Framework.Multiplayer.PlayTests
{
    public class SoloSessionTests
    {
        [UnityTest]
        public IEnumerator Solo_StartsInLobbyAsHostAtSeatZero() => UniTask.ToCoroutine(async () =>
        {
            using var session = await Session.StartSolo();

            Assert.AreEqual(SessionState.InLobby, session.State);
            Assert.AreEqual(0, session.Self.Seat);
            Assert.IsTrue(session.IsHost);
            Assert.IsTrue(session.Self.IsLocal);
            Assert.AreEqual(1, session.Peers.Count);
            Assert.AreEqual(session.Self.Seat, session.Peers[0].Seat);
        });

        [UnityTest]
        public IEnumerator Solo_SetSelfData_PopulatesPlayersThroughResolver() => UniTask.ToCoroutine(async () =>
        {
            using var session = await Session.StartSolo();

            PlayerContext received = null;
            session.PlayerContextChanged += (peer, ctx) => received = ctx;

            session.SetSelfData(new PlayerData { PlayerId = 12345 });

            Assert.IsNotNull(received);
            Assert.AreEqual(0, received.Seat);
            Assert.AreEqual(12345UL, received.PlayerId);
            Assert.IsTrue(session.Players.ContainsKey(0));
            Assert.AreEqual(12345UL, session.Players[0].PlayerId);
        });

        [UnityTest]
        public IEnumerator Solo_SetSelfReady_FlipsAllReady() => UniTask.ToCoroutine(async () =>
        {
            using var session = await Session.StartSolo();

            Assert.IsFalse(session.AllReady);

            var raised = 0;
            bool lastValue = false;
            session.ReadyChanged += (peer, ready) => { raised++; lastValue = ready; };

            session.SetSelfReady(true);

            Assert.AreEqual(1, raised);
            Assert.IsTrue(lastValue);
            Assert.IsTrue(session.ReadyStates[0]);
            Assert.IsTrue(session.AllReady);
        });

        [UnityTest]
        public IEnumerator Solo_SetSelfReady_Idempotent() => UniTask.ToCoroutine(async () =>
        {
            using var session = await Session.StartSolo();

            var raised = 0;
            session.ReadyChanged += (peer, ready) => raised++;

            session.SetSelfReady(true);
            session.SetSelfReady(true);

            Assert.AreEqual(1, raised);
        });

        [UnityTest]
        public IEnumerator Solo_StartMatch_TransitionsToInMatch() => UniTask.ToCoroutine(async () =>
        {
            using var session = await Session.StartSolo();

            SessionState? observed = null;
            session.StateChanged += s => observed = s;

            session.StartMatch();

            Assert.AreEqual(SessionState.InMatch, session.State);
            Assert.AreEqual(SessionState.InMatch, observed);
        });

        [UnityTest]
        public IEnumerator Solo_LobbyActionsAfterMatchStart_Throw() => UniTask.ToCoroutine(async () =>
        {
            using var session = await Session.StartSolo();
            session.StartMatch();

            Assert.Throws<System.InvalidOperationException>(() => session.SetSelfReady(true));
            Assert.Throws<System.InvalidOperationException>(() => session.SetSelfData(new PlayerData()));
            Assert.Throws<System.InvalidOperationException>(() => session.StartMatch());
        });

        [UnityTest]
        public IEnumerator Solo_DisposeFlipsToDisconnectedAndRaisesOnce() => UniTask.ToCoroutine(async () =>
        {
            var session = await Session.StartSolo();

            var raised = 0;
            DisconnectReason reason = default;
            session.Disconnected += r => { raised++; reason = r; };

            session.Dispose();
            session.Dispose();

            Assert.AreEqual(SessionState.Disconnected, session.State);
            Assert.AreEqual(1, raised);
            Assert.AreEqual(DisconnectReason.LocalShutdown, reason);
        });
    }
}
