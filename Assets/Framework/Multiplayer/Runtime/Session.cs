using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Framework.Wire;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace Framework.Multiplayer
{
    public class Session<TData, TContext> : IDisposable
        where TData    : PlayerData,    new()
        where TContext : PlayerContext, new()
    {
        const float JoinTimeoutSeconds = 10f;
        const string LobbyMessageName = "Framework.Multiplayer";

        readonly List<Peer> _peers = new();
        readonly Dictionary<int, TContext> _players = new();
        readonly Dictionary<int, bool> _ready = new();
        bool _disposed;

        NetworkManager _nm;
        NgoTransport _transport;
        Network _network;
        ContextResolver<TData, TContext> _contextResolver;

        Channel<PeerWelcomeEvent> _welcomeCh;
        Channel<PeerJoinedEvent> _joinedCh;
        Channel<PeerLeftEvent> _leftCh;
        Channel<PlayerDataEvent> _playerDataCh;
        Channel<PlayerContextEvent> _playerContextCh;
        Channel<PeerReadyChangedEvent> _readyCh;
        Channel<MatchStartEvent> _matchStartCh;

        Action<ulong> _hostConnectHandler;
        Action<ulong> _hostDisconnectHandler;
        Action<ulong> _clientDisconnectHandler;

        int _nextSeat = 1;

        bool _isHost;

        public NetworkManager NetworkManager => _nm;
        public Peer Self { get; private set; }
        public IReadOnlyList<Peer> Peers => _peers;
        // Sourced from a flag set at factory time, not Self.IsHost — Self is default(Peer)
        // on clients during Connecting (Seat == 0), which would otherwise misroute host-only
        // handlers like the PlayerData resolver path.
        public bool IsHost => _isHost;
        public SessionState State { get; private set; }
        public IReadOnlyDictionary<int, TContext> Players => _players;
        public IReadOnlyDictionary<int, bool> ReadyStates => _ready;

        public bool AllReady
        {
            get
            {
                if (_peers.Count == 0) return false;
                foreach (var peer in _peers)
                {
                    if (!_ready.TryGetValue(peer.Seat, out var r) || !r) return false;
                }
                return true;
            }
        }

        public event Action<Peer> PeerJoined;
        public event Action<Peer> PeerLeft;
        public event Action<Peer, TContext> PlayerContextChanged;
        public event Action<Peer, bool> ReadyChanged;
        public event Action<SessionState> StateChanged;
        public event Action<DisconnectReason> Disconnected;

        protected Session() { }

        // === Factories ===

        public static UniTask<Session<TData, TContext>> StartSolo(
            ContextResolver<TData, TContext> contextResolver = null)
        {
            var s = new Session<TData, TContext>
            {
                _contextResolver = contextResolver ?? DefaultResolver,
                _isHost = true,
            };
            s._network = new Network(LoopbackTransport.Solo());
            s.RegisterChannels();
            s.SubscribeChannels();

            s.Self = new Peer(seat: 0, networkId: 0, isLocal: true);
            s._peers.Add(s.Self);
            s.TransitionTo(SessionState.InLobby);
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

            var go = new GameObject("Framework.Multiplayer.NetworkManager");
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

        public static UniTask<Session<TData, TContext>> StartHostDirect(
            ushort port,
            string listenAddress = "0.0.0.0",
            ContextResolver<TData, TContext> contextResolver = null)
        {
            var (nm, utp) = CreateNetworkManager();
            utp.SetConnectionData(listenAddress, port, listenAddress);
            return StartHost(nm, contextResolver);
        }

        public static UniTask<Session<TData, TContext>> JoinDirect(string ip, ushort port)
        {
            var (nm, utp) = CreateNetworkManager();
            utp.SetConnectionData(ip, port);
            return Join(nm);
        }

        public static UniTask<Session<TData, TContext>> StartHost(
            NetworkManager nm,
            ContextResolver<TData, TContext> contextResolver = null)
        {
            if (nm == null) throw new ArgumentNullException(nameof(nm));

            var s = new Session<TData, TContext>
            {
                _nm = nm,
                _contextResolver = contextResolver ?? DefaultResolver,
                _isHost = true,
            };

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

            s._transport = new NgoTransport(nm, LobbyMessageName);
            s._network = new Network(s._transport);
            s.RegisterChannels();
            s.SubscribeChannels();

            s.Self = new Peer(seat: 0, networkId: nm.LocalClientId, isLocal: true);
            s._peers.Add(s.Self);
            s.TransitionTo(SessionState.InLobby);

            return UniTask.FromResult(s);
        }

        public static async UniTask<Session<TData, TContext>> Join(NetworkManager nm)
        {
            if (nm == null) throw new ArgumentNullException(nameof(nm));

            var s = new Session<TData, TContext>
            {
                _nm = nm,
                _contextResolver = DefaultResolver,   // unused on clients, set for completeness
            };

            s._clientDisconnectHandler = s.ClientOnClientDisconnected;
            nm.OnClientDisconnectCallback += s._clientDisconnectHandler;

            if (!nm.StartClient())
            {
                nm.OnClientDisconnectCallback -= s._clientDisconnectHandler;
                throw new SessionStartupException("NetworkManager.StartClient() returned false");
            }

            s.TransitionTo(SessionState.Connecting);

            // Register the named-message handler synchronously so the host's first sends are not lost.
            s._transport = new NgoTransport(nm, LobbyMessageName);
            s._network = new Network(s._transport);
            s.RegisterChannels();
            s.SubscribeChannels();

            try
            {
                await UniTask.WaitUntil(() => s.State == SessionState.InLobby || s.State == SessionState.Disconnected)
                    .Timeout(TimeSpan.FromSeconds(JoinTimeoutSeconds));
            }
            catch (TimeoutException ex)
            {
                s.TearDownNgo();
                throw new SessionJoinException(JoinFailureKind.Timeout, "Did not receive PeerWelcomeEvent within timeout", ex);
            }

            if (s.State != SessionState.InLobby)
            {
                s.TearDownNgo();
                throw new SessionJoinException(JoinFailureKind.NgoConnectFailed, "Connection failed before Welcome arrived");
            }

            return s;
        }

        // === Channel setup ===

        void RegisterChannels()
        {
            // Order is the discriminator contract — host and joiner must register the same types in the same order.
            _welcomeCh       = _network.Register<PeerWelcomeEvent>();
            _joinedCh        = _network.Register<PeerJoinedEvent>();
            _leftCh          = _network.Register<PeerLeftEvent>();
            _playerDataCh    = _network.Register<PlayerDataEvent>();
            _playerContextCh = _network.Register<PlayerContextEvent>();
            _readyCh         = _network.Register<PeerReadyChangedEvent>();
            _matchStartCh    = _network.Register<MatchStartEvent>();
        }

        void SubscribeChannels()
        {
            _welcomeCh.OnReceived       += OnWelcomeReceived;
            _joinedCh.OnReceived        += OnPeerJoinedReceived;
            _leftCh.OnReceived          += OnPeerLeftReceived;
            _playerDataCh.OnReceived    += OnPlayerDataReceived;
            _playerContextCh.OnReceived += OnPlayerContextReceived;
            _readyCh.OnReceived         += OnReadyReceived;
            _matchStartCh.OnReceived    += OnMatchStartReceived;
        }

        static TContext DefaultResolver(TData data)
        {
            var ctx = new TContext
            {
                NetworkId = data.NetworkId,
                PlayerId  = data.PlayerId,
            };
            return ctx;
        }

        // === Lobby actions ===

        public void SetSelfData(TData data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            RequireInLobby(nameof(SetSelfData));

            // Stamp the submitter's NetworkId — apps can leave it default; we fill from Self.
            data.NetworkId = Self.NetworkId;
            _network.Send(new PlayerDataEvent
            {
                Seat      = Self.Seat,
                NetworkId = data.NetworkId,
                PlayerId  = data.PlayerId,
            });
        }

        public void SetSelfReady(bool ready)
        {
            RequireInLobby(nameof(SetSelfReady));
            _network.Send(new PeerReadyChangedEvent { Seat = Self.Seat, Ready = ready });
        }

        public void StartMatch()
        {
            if (!IsHost) throw new InvalidOperationException("StartMatch is host-only");
            RequireInLobby(nameof(StartMatch));
            _network.Send(new MatchStartEvent());
        }

        void RequireInLobby(string op)
        {
            if (State != SessionState.InLobby)
                throw new InvalidOperationException($"{op} requires SessionState.InLobby (got {State})");
        }

        // === Host-side NGO callbacks ===

        void HostOnClientConnected(ulong clientId)
        {
            if (clientId == _nm.LocalClientId) return;

            // Backfill — tell the new joiner about every existing peer (including host).
            foreach (var existing in _peers)
            {
                _network.Send(new PeerJoinedEvent { Seat = existing.Seat, NetworkId = existing.NetworkId });
            }

            var seat = _nextSeat++;
            var peer = new Peer(seat: seat, networkId: clientId, isLocal: false);
            _peers.Add(peer);

            _network.Send(new PeerWelcomeEvent { YourSeat = seat, YourNetworkId = clientId });
            _network.Send(new PeerJoinedEvent { Seat = seat, NetworkId = clientId });

            // Replay roster's known PlayerContext to the new joiner so they catch up on
            // who's already submitted what.
            foreach (var entry in _players)
            {
                var ctx = entry.Value;
                _network.Send(new PlayerContextEvent
                {
                    Seat      = ctx.Seat,
                    NetworkId = ctx.NetworkId,
                    PlayerId  = ctx.PlayerId,
                });
            }

            // Likewise replay current ready states.
            foreach (var entry in _ready)
            {
                _network.Send(new PeerReadyChangedEvent { Seat = entry.Key, Ready = entry.Value });
            }

            PeerJoined?.Invoke(peer);
        }

        void HostOnClientDisconnected(ulong clientId)
        {
            var idx = _peers.FindIndex(p => p.NetworkId == clientId);
            if (idx < 0) return;
            var peer = _peers[idx];
            _peers.RemoveAt(idx);
            _players.Remove(peer.Seat);
            _ready.Remove(peer.Seat);

            _network.Send(new PeerLeftEvent { Seat = peer.Seat });
            PeerLeft?.Invoke(peer);
        }

        // === Client-side NGO callbacks ===

        void ClientOnClientDisconnected(ulong clientId)
        {
            if (clientId != _nm.LocalClientId) return;
            if (State == SessionState.Disconnected) return;

            var reason = string.IsNullOrEmpty(_nm.DisconnectReason)
                ? DisconnectReason.NetworkError
                : DisconnectReason.HostShutdown;
            TransitionTo(SessionState.Disconnected);
            Disconnected?.Invoke(reason);
        }

        // === Receive handlers ===

        void OnWelcomeReceived(PeerWelcomeEvent ev)
        {
            if (ev.YourNetworkId != _nm.LocalClientId) return;
            if (State != SessionState.Connecting) return;

            Self = new Peer(seat: ev.YourSeat, networkId: ev.YourNetworkId, isLocal: true);
            _peers.Add(Self);
            TransitionTo(SessionState.InLobby);
        }

        void OnPeerJoinedReceived(PeerJoinedEvent ev)
        {
            if (_nm != null && ev.NetworkId == _nm.LocalClientId) return;     // self-echo
            if (_peers.Exists(p => p.Seat == ev.Seat)) return;                // already known

            var peer = new Peer(seat: ev.Seat, networkId: ev.NetworkId, isLocal: false);
            _peers.Add(peer);

            // Backfill events arriving during Connecting populate the roster silently.
            if (State == SessionState.InLobby)
                PeerJoined?.Invoke(peer);
        }

        void OnPeerLeftReceived(PeerLeftEvent ev)
        {
            var idx = _peers.FindIndex(p => p.Seat == ev.Seat);
            if (idx < 0) return;
            var peer = _peers[idx];
            _peers.RemoveAt(idx);
            _players.Remove(ev.Seat);
            _ready.Remove(ev.Seat);
            PeerLeft?.Invoke(peer);
        }

        void OnPlayerDataReceived(PlayerDataEvent ev)
        {
            // Only the host resolves submissions and broadcasts the authoritative context.
            if (!IsHost) return;
            // Ignore submissions for seats not in the roster (defensive — shouldn't happen).
            if (!_peers.Exists(p => p.Seat == ev.Seat)) return;

            var submission = new TData
            {
                NetworkId = ev.NetworkId,
                PlayerId  = ev.PlayerId,
            };
            var resolved = _contextResolver(submission) ?? DefaultResolver(submission);
            // Host stamps the seat — clients can't lie about where they sit.
            resolved.Seat = ev.Seat;
            resolved.NetworkId = ev.NetworkId;

            _network.Send(new PlayerContextEvent
            {
                Seat      = resolved.Seat,
                NetworkId = resolved.NetworkId,
                PlayerId  = resolved.PlayerId,
            });
        }

        void OnPlayerContextReceived(PlayerContextEvent ev)
        {
            var ctx = new TContext
            {
                NetworkId = ev.NetworkId,
                Seat      = ev.Seat,
                PlayerId  = ev.PlayerId,
            };
            _players[ev.Seat] = ctx;

            var peer = FindPeer(ev.Seat);
            PlayerContextChanged?.Invoke(peer, ctx);
        }

        void OnReadyReceived(PeerReadyChangedEvent ev)
        {
            var wasInRoster = _ready.TryGetValue(ev.Seat, out var prev);
            var changed = !wasInRoster || prev != ev.Ready;
            if (!changed) return;     // idempotent — also terminates the host re-broadcast loop

            _ready[ev.Seat] = ev.Ready;
            var peer = FindPeer(ev.Seat);
            ReadyChanged?.Invoke(peer, ev.Ready);

            // Host re-broadcasts so other clients hear the update. Self-echo of the rebroadcast
            // hits the change filter above and stops.
            if (IsHost)
                _network.Send(new PeerReadyChangedEvent { Seat = ev.Seat, Ready = ev.Ready });
        }

        void OnMatchStartReceived(MatchStartEvent ev)
        {
            if (State != SessionState.InLobby) return;
            TransitionTo(SessionState.InMatch);
        }

        // === Helpers ===

        Peer FindPeer(int seat)
        {
            foreach (var p in _peers) if (p.Seat == seat) return p;
            return default;
        }

        void TransitionTo(SessionState newState)
        {
            if (State == newState) return;
            State = newState;
            StateChanged?.Invoke(newState);
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
                TransitionTo(SessionState.Disconnected);
                Disconnected?.Invoke(DisconnectReason.LocalShutdown);
            }

            TearDownNgo();
        }
    }

    // Non-generic facade for the common case (no game-specific PlayerData/PlayerContext extension).
    // Deviation from the spike doc's `sealed class Session : Session<PlayerData, PlayerContext>`
    // pattern: inheritance would propagate the generic class's static return types
    // (UniTask<Session<PlayerData, PlayerContext>>) verbatim, which is fine but reads as a leaky
    // abstraction. A static facade keeps the call sites clean.
    public static class Session
    {
        public static UniTask<Session<PlayerData, PlayerContext>> StartSolo()
            => Session<PlayerData, PlayerContext>.StartSolo();

        public static UniTask<Session<PlayerData, PlayerContext>> StartHostDirect(
            ushort port, string listenAddress = "0.0.0.0")
            => Session<PlayerData, PlayerContext>.StartHostDirect(port, listenAddress);

        public static UniTask<Session<PlayerData, PlayerContext>> JoinDirect(string ip, ushort port)
            => Session<PlayerData, PlayerContext>.JoinDirect(ip, port);

        public static UniTask<Session<PlayerData, PlayerContext>> StartHost(NetworkManager nm)
            => Session<PlayerData, PlayerContext>.StartHost(nm);

        public static UniTask<Session<PlayerData, PlayerContext>> Join(NetworkManager nm)
            => Session<PlayerData, PlayerContext>.Join(nm);

        public static (NetworkManager nm, UnityTransport utp) CreateNetworkManager()
            => Session<PlayerData, PlayerContext>.CreateNetworkManager();
    }
}
