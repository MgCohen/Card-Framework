using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Framework.Wire;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace Framework.Session
{
    public sealed class Session : IDisposable
    {
        const float JoinTimeoutSeconds = 10f;

        readonly List<Peer> _peers = new();
        bool _disposed;

        NetworkManager _nm;
        NgoTransport _transport;
        Channel<PeerWelcomeEvent> _welcomeCh;
        Channel<PeerJoinedEvent> _joinedCh;
        Channel<PeerLeftEvent> _leftCh;

        Action<ulong> _hostConnectHandler;
        Action<ulong> _hostDisconnectHandler;
        Action<ulong> _clientDisconnectHandler;

        int _nextSeat = 1;

        public Network Network { get; private set; }
        public EventStream Stream { get; private set; }
        public Peer Self { get; private set; }
        public IReadOnlyList<Peer> Peers => _peers;
        public bool IsHost => Self.IsHost;
        public SessionState State { get; private set; }

        public event Action<Peer> PeerJoined;
        public event Action<Peer> PeerLeft;
        public event Action<DisconnectReason> Disconnected;

        Session() { }

        public static UniTask<Session> StartSolo()
        {
            var s = new Session();
            s.Network = new Network(LoopbackTransport.Solo());
            s.Stream = new EventStream();
            s.Network.OnReceived += s.Stream.Add;

            // Reserve channel ids 0-2 for membership events so user channels start at id 3
            // identically across all modes. In solo nobody else is listening, but the
            // discriminator alignment stays consistent for any future Network swap.
            s.Network.Register<PeerWelcomeEvent>();
            s.Network.Register<PeerJoinedEvent>();
            s.Network.Register<PeerLeftEvent>();

            s.Self = new Peer(seat: 0, networkId: 0, isLocal: true);
            s._peers.Add(s.Self);
            s.State = SessionState.Connected;
            return UniTask.FromResult(s);
        }

        public static (NetworkManager nm, UnityTransport utp) CreateNetworkManager()
        {
            if (NetworkManager.Singleton != null)
            {
                throw new SessionStartupException(
                    "A NetworkManager.Singleton already exists. Session creates and owns its own NetworkManager; " +
                    "tear down the existing one before starting a Session.");
            }

            var go = new GameObject("Framework.Session.NetworkManager");
            UnityEngine.Object.DontDestroyOnLoad(go);
            var nm = go.AddComponent<NetworkManager>();
            var utp = go.AddComponent<UnityTransport>();
            nm.NetworkConfig = new NetworkConfig
            {
                NetworkTransport = utp,
                TickRate = 30,
            };
            return (nm, utp);
        }

        public static UniTask<Session> StartHostDirect(ushort port, string listenAddress = "0.0.0.0")
        {
            var (nm, utp) = CreateNetworkManager();
            utp.SetConnectionData(listenAddress, port, listenAddress);
            return StartHost(nm);
        }

        public static UniTask<Session> JoinDirect(string ip, ushort port)
        {
            var (nm, utp) = CreateNetworkManager();
            utp.SetConnectionData(ip, port);
            return Join(nm);
        }

        public static UniTask<Session> StartHost(NetworkManager nm)
        {
            if (nm == null) throw new ArgumentNullException(nameof(nm));

            var s = new Session { _nm = nm };

            s._hostConnectHandler = s.HostOnClientConnected;
            s._hostDisconnectHandler = s.HostOnClientDisconnected;
            nm.OnClientConnectedCallback += s._hostConnectHandler;
            nm.OnClientDisconnectCallback += s._hostDisconnectHandler;

            if (!nm.StartHost())
            {
                nm.OnClientConnectedCallback -= s._hostConnectHandler;
                nm.OnClientDisconnectCallback -= s._hostDisconnectHandler;
                throw new SessionStartupException("NetworkManager.StartHost() returned false");
            }

            s._transport = new NgoTransport(nm);
            s.Network = new Network(s._transport);
            s.Stream = new EventStream();
            s.Network.OnReceived += s.Stream.Add;
            s._welcomeCh = s.Network.Register<PeerWelcomeEvent>();
            s._joinedCh = s.Network.Register<PeerJoinedEvent>();
            s._leftCh = s.Network.Register<PeerLeftEvent>();
            s._welcomeCh.OnReceived += s.OnWelcomeReceived;
            s._joinedCh.OnReceived += s.OnPeerJoinedReceived;
            s._leftCh.OnReceived += s.OnPeerLeftReceived;

            s.Self = new Peer(seat: 0, networkId: nm.LocalClientId, isLocal: true);
            s._peers.Add(s.Self);
            s.State = SessionState.Connected;

            return UniTask.FromResult(s);
        }

        public static async UniTask<Session> Join(NetworkManager nm)
        {
            if (nm == null) throw new ArgumentNullException(nameof(nm));

            var s = new Session { _nm = nm };

            s._clientDisconnectHandler = s.ClientOnClientDisconnected;
            nm.OnClientDisconnectCallback += s._clientDisconnectHandler;

            if (!nm.StartClient())
            {
                nm.OnClientDisconnectCallback -= s._clientDisconnectHandler;
                throw new SessionStartupException("NetworkManager.StartClient() returned false");
            }

            s.State = SessionState.Connecting;

            // Register the named-message handler synchronously so any message arriving after the
            // NGO handshake is caught — the host may start sending Welcome/PeerJoined the instant
            // its OnClientConnectedCallback fires, which can be before our next async continuation.
            s._transport = new NgoTransport(nm);
            s.Network = new Network(s._transport);
            s.Stream = new EventStream();
            s.Network.OnReceived += s.Stream.Add;
            s._welcomeCh = s.Network.Register<PeerWelcomeEvent>();
            s._joinedCh = s.Network.Register<PeerJoinedEvent>();
            s._leftCh = s.Network.Register<PeerLeftEvent>();
            s._welcomeCh.OnReceived += s.OnWelcomeReceived;
            s._joinedCh.OnReceived += s.OnPeerJoinedReceived;
            s._leftCh.OnReceived += s.OnPeerLeftReceived;

            try
            {
                await UniTask.WaitUntil(() => s.State == SessionState.Connected || s.State == SessionState.Disconnected)
                    .Timeout(TimeSpan.FromSeconds(JoinTimeoutSeconds));
            }
            catch (TimeoutException ex)
            {
                s.TearDownNgo();
                throw new SessionJoinException(JoinFailureKind.Timeout, "Did not receive PeerWelcomeEvent within timeout", ex);
            }

            if (s.State != SessionState.Connected)
            {
                s.TearDownNgo();
                throw new SessionJoinException(JoinFailureKind.NgoConnectFailed, "Connection failed before Welcome arrived");
            }

            return s;
        }

        void HostOnClientConnected(ulong clientId)
        {
            if (clientId == _nm.LocalClientId) return;

            // Backfill: announce every existing peer to the new joiner. Existing peers also
            // receive these and drop them (already in roster); host filters its own self-echo.
            foreach (var existing in _peers)
            {
                Network.Send(new PeerJoinedEvent { Seat = existing.Seat, NetworkId = existing.NetworkId });
            }

            var seat = _nextSeat++;
            var peer = new Peer(seat: seat, networkId: clientId, isLocal: false);
            _peers.Add(peer);

            Network.Send(new PeerWelcomeEvent { YourSeat = seat, YourNetworkId = clientId });
            Network.Send(new PeerJoinedEvent { Seat = seat, NetworkId = clientId });

            PeerJoined?.Invoke(peer);
        }

        void HostOnClientDisconnected(ulong clientId)
        {
            var idx = _peers.FindIndex(p => p.NetworkId == clientId);
            if (idx < 0) return;
            var peer = _peers[idx];
            _peers.RemoveAt(idx);

            Network.Send(new PeerLeftEvent { Seat = peer.Seat });
            PeerLeft?.Invoke(peer);
        }

        void ClientOnClientDisconnected(ulong clientId)
        {
            if (clientId != _nm.LocalClientId) return;
            if (State == SessionState.Disconnected) return;

            var reason = string.IsNullOrEmpty(_nm.DisconnectReason)
                ? DisconnectReason.NetworkError
                : DisconnectReason.HostShutdown;
            State = SessionState.Disconnected;
            Disconnected?.Invoke(reason);
        }

        void OnWelcomeReceived(PeerWelcomeEvent ev)
        {
            if (ev.YourNetworkId != _nm.LocalClientId) return;
            if (State != SessionState.Connecting) return;

            Self = new Peer(seat: ev.YourSeat, networkId: ev.YourNetworkId, isLocal: true);
            _peers.Add(Self);
            State = SessionState.Connected;
        }

        void OnPeerJoinedReceived(PeerJoinedEvent ev)
        {
            if (ev.NetworkId == _nm.LocalClientId) return;
            if (_peers.Exists(p => p.Seat == ev.Seat)) return;

            var peer = new Peer(seat: ev.Seat, networkId: ev.NetworkId, isLocal: false);
            _peers.Add(peer);

            // Backfill events arriving during Connecting populate the roster silently — the
            // pre-existing peer list is just facts queryable via Peers once Connected fires.
            if (State == SessionState.Connected)
                PeerJoined?.Invoke(peer);
        }

        void OnPeerLeftReceived(PeerLeftEvent ev)
        {
            var idx = _peers.FindIndex(p => p.Seat == ev.Seat);
            if (idx < 0) return;
            var peer = _peers[idx];
            _peers.RemoveAt(idx);
            PeerLeft?.Invoke(peer);
        }

        void TearDownNgo()
        {
            if (_nm == null) return;

            if (_hostConnectHandler != null)
            {
                _nm.OnClientConnectedCallback -= _hostConnectHandler;
                _hostConnectHandler = null;
            }
            if (_hostDisconnectHandler != null)
            {
                _nm.OnClientDisconnectCallback -= _hostDisconnectHandler;
                _hostDisconnectHandler = null;
            }
            if (_clientDisconnectHandler != null)
            {
                _nm.OnClientDisconnectCallback -= _clientDisconnectHandler;
                _clientDisconnectHandler = null;
            }

            _transport?.Dispose();
            _transport = null;

            if (_nm.IsListening) _nm.Shutdown();
            UnityEngine.Object.Destroy(_nm.gameObject);
            _nm = null;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            if (State != SessionState.Disconnected)
            {
                State = SessionState.Disconnected;
                Disconnected?.Invoke(DisconnectReason.LocalShutdown);
            }

            TearDownNgo();
        }
    }
}
