using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Framework.Multiplayer.Sample
{
    public class LobbyHarness : MonoBehaviour
    {
        Session<PlayerData, PlayerContext> _session;
        string _ip = "127.0.0.1";
        string _port = "7787";
        string _playerId = "1";

        void OnDestroy() => _session?.Dispose();

        void OnGUI()
        {
            GUILayout.BeginArea(new Rect(20, 20, 360, 600));
            GUILayout.Label($"Status: {(_session == null ? "idle" : _session.State.ToString())}");

            if (_session == null) DrawConnectPanel();
            else if (_session.State == SessionState.InLobby) DrawLobbyPanel();
            else if (_session.State == SessionState.InMatch) DrawMatchPanel();
            else DrawDisconnectedPanel();

            GUILayout.EndArea();
        }

        void DrawConnectPanel()
        {
            if (GUILayout.Button("Host Solo")) StartSolo().Forget();

            GUILayout.Label("Port:");
            _port = GUILayout.TextField(_port);
            if (GUILayout.Button("Host LAN")) HostLan().Forget();

            GUILayout.Label("Host IP:");
            _ip = GUILayout.TextField(_ip);
            if (GUILayout.Button("Join LAN")) JoinLan().Forget();
        }

        void DrawLobbyPanel()
        {
            GUILayout.Label($"Seat: {_session.Self.Seat}  (host={_session.IsHost})");

            GUILayout.Label("PlayerId:");
            _playerId = GUILayout.TextField(_playerId);
            if (GUILayout.Button("Submit data"))
                _session.SetSelfData(new PlayerData { PlayerId = ulong.Parse(_playerId) });

            var ready = _session.ReadyStates.TryGetValue(_session.Self.Seat, out var r) && r;
            if (GUILayout.Button(ready ? "Unready" : "Ready"))
                _session.SetSelfReady(!ready);

            GUILayout.Label($"Peers ({_session.Peers.Count}):");
            foreach (var peer in _session.Peers)
            {
                var pid = _session.Players.TryGetValue(peer.Seat, out var ctx) ? ctx.PlayerId.ToString() : "-";
                var rr = _session.ReadyStates.TryGetValue(peer.Seat, out var rv) && rv;
                GUILayout.Label($"  seat {peer.Seat}  id={pid}  ready={rr}");
            }

            if (_session.IsHost)
            {
                GUI.enabled = _session.AllReady;
                if (GUILayout.Button("Start Match")) _session.StartMatch();
                GUI.enabled = true;
            }

            if (GUILayout.Button("Leave")) Leave();
        }

        void DrawMatchPanel()
        {
            GUILayout.Label("Match started — gameplay layer takes over.");
            if (GUILayout.Button("Leave")) Leave();
        }

        void DrawDisconnectedPanel()
        {
            GUILayout.Label("Disconnected.");
            if (GUILayout.Button("Reset")) Leave();
        }

        async UniTask StartSolo() => _session = await Session.StartSolo();
        async UniTask HostLan() => _session = await Session.StartHostDirect(ushort.Parse(_port));
        async UniTask JoinLan() => _session = await Session.JoinDirect(_ip, ushort.Parse(_port));

        void Leave()
        {
            _session?.Dispose();
            _session = null;
        }
    }
}
