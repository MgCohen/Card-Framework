using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Framework.Wire;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace Framework.Multiplayer
{
    public sealed class Session<TData, TContext> : IDisposable
        where TData    : PlayerData,    new()
        where TContext : PlayerContext, new()
    {
        const float JoinTimeoutSeconds = 10f;
        const string LobbyMessageName = "Framework.Multiplayer";

        readonly PeerRoster<TContext> _roster = new();

        NetworkManager _nm;
        NgoTransport _transport;
        Network _network;
        ContextResolver<TData, TContext> _contextResolver;

        NetworkChannel<PeerWelcomeEvent> _welcomeCh;
        NetworkChannel<PeerJoinedEvent> _joinedCh;
        NetworkChannel<PeerLeftEvent> _leftCh;
        NetworkChannel<PlayerDataEvent> _playerDataCh;
        NetworkChannel<PlayerContextEvent> _playerContextCh;
        NetworkChannel<PeerReadyChangedEvent> _readyCh;
        NetworkChannel<MatchStartEvent> _matchStartCh;

        Action<ulong> _hostConnectHandler;
        Action<ulong> _hostDisconnectHandler;
        Action<ulong> _clientDisconnectHandler;

        bool _isHost;
        bool _disposed;

        public NetworkManager NetworkManager => _nm;
        public Peer Self { get; private set; }
        public IReadOnlyList<Peer> Peers => _roster.Peers;
        public bool IsHost => _isHost;
        public SessionState State { get; private set; }
        public IReadOnlyDictionary<int, TContext> Players => _roster.Players;
        public IReadOnlyDictionary<int, bool> ReadyStates => _roster.ReadyStates;
        public bool AllReady => _roster.AllReady;

        public event Action<Peer> PeerJoined;
        public event Action<Peer> PeerLeft;
        public event Action<Peer, TContext> PlayerContextChanged;
        public event Action<Peer, bool> ReadyChanged;
        public event Action<SessionState> StateChanged;
        public event Action<DisconnectReason> Disconnected;

        private Session() { }

        public static UniTask<Session<TData, TContext>> StartSolo(
            ContextResolver<TData, TContext> contextResolver = null)
        {
            var s = new Session<TData, TContext>
            {
                _contextResolver = contextResolver ?? DefaultResolver,
                _isHost = true,
            };
            s.Bind(new Network(LoopbackTransport.Solo()));

            s.Self = new Peer(seat: 0, networkId: 0, isLocal: true);
            s._roster.Add(s.Self);
            s.TransitionTo(SessionState.InLobby);
            return UniTask.FromResult(s);
        }

        public static NetworkManagerSetup CreateNetworkManager()
        {
            if (NetworkManager.Singleton != null)
                throw new SessionStartupException("NetworkManager already exists");

            var go = new GameObject("Framework.Multiplayer.NetworkManager");
            UnityEngine.Object.DontDestroyOnLoad(go);
            var nm = go.AddComponent<NetworkManager>();
            var utp = go.AddComponent<UnityTransport>();
            nm.NetworkConfig = new NetworkConfig
            {
                NetworkTransport = utp,
                TickRate = 30,
            };
            return new NetworkManagerSetup(nm, utp);
        }

        public static UniTask<Session<TData, TContext>> StartHostDirect(
            ushort port,
            string listenAddress = "0.0.0.0",
            ContextResolver<TData, TContext> contextResolver = null)
        {
            var setup = CreateNetworkManager();
            setup.Transport.SetConnectionData(listenAddress, port, listenAddress);
            return StartHost(setup.Manager, contextResolver);
        }

        public static UniTask<Session<TData, TContext>> JoinDirect(string ip, ushort port)
        {
            var setup = CreateNetworkManager();
            setup.Transport.SetConnectionData(ip, port);
            return Join(setup.Manager);
        }

        public static UniTask<Session<TData, TContext>> StartHost(
            NetworkManager nm,
            ContextResolver<TData, TContext> contextResolver = null)
        {
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

            s.BindNgo(nm);

            s.Self = new Peer(seat: 0, networkId: nm.LocalClientId, isLocal: true);
            s._roster.Add(s.Self);
            s.TransitionTo(SessionState.InLobby);

            return UniTask.FromResult(s);
        }

        public static async UniTask<Session<TData, TContext>> Join(NetworkManager nm)
        {
            var s = new Session<TData, TContext>
            {
                _nm = nm,
                _contextResolver = DefaultResolver,
            };

            s._clientDisconnectHandler = s.ClientOnClientDisconnected;
            nm.OnClientDisconnectCallback += s._clientDisconnectHandler;

            if (!nm.StartClient())
            {
                nm.OnClientDisconnectCallback -= s._clientDisconnectHandler;
                throw new SessionStartupException("NetworkManager.StartClient() returned false");
            }

            s.TransitionTo(SessionState.Connecting);

            // Register synchronously so the host's first sends aren't lost.
            s.BindNgo(nm);

            try
            {
                await UniTask.WaitUntil(() => s.State == SessionState.InLobby || s.State == SessionState.Disconnected)
                    .Timeout(TimeSpan.FromSeconds(JoinTimeoutSeconds));
            }
            catch (TimeoutException ex)
            {
                s.TearDownNgo();
                throw new SessionJoinException(JoinFailureKind.Timeout, "Welcome timeout", ex);
            }

            if (s.State != SessionState.InLobby)
            {
                s.TearDownNgo();
                throw new SessionJoinException(JoinFailureKind.NgoConnectFailed, "Disconnected before Welcome");
            }

            return s;
        }

        void BindNgo(NetworkManager nm)
        {
            _transport = new NgoTransport(nm, LobbyMessageName);
            Bind(new Network(_transport));
        }

        void Bind(Network network)
        {
            _network = network;
            RegisterChannels();
            SubscribeChannels();
        }

        void RegisterChannels()
        {
            _welcomeCh       = _network.Register<PeerWelcomeEvent, PeerWelcomeEvent.Packed>();
            _joinedCh        = _network.Register<PeerJoinedEvent, PeerJoinedEvent.Packed>();
            _leftCh          = _network.Register<PeerLeftEvent, PeerLeftEvent.Packed>();
            _playerDataCh    = _network.Register<PlayerDataEvent, PlayerDataEvent.Packed>();
            _playerContextCh = _network.Register<PlayerContextEvent, PlayerContextEvent.Packed>();
            _readyCh         = _network.Register<PeerReadyChangedEvent, PeerReadyChangedEvent.Packed>();
            _matchStartCh    = _network.Register<MatchStartEvent, MatchStartEvent.Packed>();
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

        static TContext DefaultResolver(TData data) => new TContext
        {
            NetworkId = data.NetworkId,
            PlayerId  = data.PlayerId,
        };

        public void SetSelfData(TData data)
        {
            if (State != SessionState.InLobby) throw new InvalidOperationException("Not in lobby");
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
            if (State != SessionState.InLobby) throw new InvalidOperationException("Not in lobby");
            _network.Send(new PeerReadyChangedEvent { Seat = Self.Seat, Ready = ready });
        }

        public void StartMatch()
        {
            if (!_isHost) throw new InvalidOperationException("Host only");
            if (State != SessionState.InLobby) throw new InvalidOperationException("Not in lobby");
            _network.Send(new MatchStartEvent());
        }

        void HostOnClientConnected(ulong clientId)
        {
            if (clientId == _nm.LocalClientId) return;

            foreach (var existing in _roster.Peers)
                _network.Send(new PeerJoinedEvent { Seat = existing.Seat, NetworkId = existing.NetworkId });

            var seat = _roster.NextSeat();
            var peer = new Peer(seat: seat, networkId: clientId, isLocal: false);
            _roster.Add(peer);

            _network.Send(new PeerWelcomeEvent { YourSeat = seat, YourNetworkId = clientId });
            _network.Send(new PeerJoinedEvent { Seat = seat, NetworkId = clientId });

            foreach (var ctx in _roster.Players.Values)
                _network.Send(new PlayerContextEvent { Seat = ctx.Seat, NetworkId = ctx.NetworkId, PlayerId = ctx.PlayerId });

            foreach (var entry in _roster.ReadyStates)
                _network.Send(new PeerReadyChangedEvent { Seat = entry.Key, Ready = entry.Value });

            PeerJoined?.Invoke(peer);
        }

        void HostOnClientDisconnected(ulong clientId)
        {
            if (!_roster.TryGetByNetworkId(clientId, out var peer)) return;
            _roster.Remove(peer.Seat, out _);

            _network.Send(new PeerLeftEvent { Seat = peer.Seat });
            PeerLeft?.Invoke(peer);
        }

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

        void OnWelcomeReceived(PeerWelcomeEvent ev)
        {
            if (ev.YourNetworkId != _nm.LocalClientId) return;
            if (State != SessionState.Connecting) return;

            Self = new Peer(seat: ev.YourSeat, networkId: ev.YourNetworkId, isLocal: true);
            _roster.Add(Self);
            TransitionTo(SessionState.InLobby);
        }

        void OnPeerJoinedReceived(PeerJoinedEvent ev)
        {
            if (_nm != null && ev.NetworkId == _nm.LocalClientId) return;
            if (_roster.TryGetBySeat(ev.Seat, out _)) return;

            var peer = new Peer(seat: ev.Seat, networkId: ev.NetworkId, isLocal: false);
            _roster.Add(peer);

            if (State == SessionState.InLobby)
                PeerJoined?.Invoke(peer);
        }

        void OnPeerLeftReceived(PeerLeftEvent ev)
        {
            if (!_roster.Remove(ev.Seat, out var peer)) return;
            PeerLeft?.Invoke(peer);
        }

        void OnPlayerDataReceived(PlayerDataEvent ev)
        {
            if (!_isHost) return;

            var submission = new TData { NetworkId = ev.NetworkId, PlayerId = ev.PlayerId };
            var resolved = _contextResolver(submission);
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
            _roster.SetContext(ev.Seat, ctx);
            _roster.TryGetBySeat(ev.Seat, out var peer);
            PlayerContextChanged?.Invoke(peer, ctx);
        }

        void OnReadyReceived(PeerReadyChangedEvent ev)
        {
            if (_roster.ReadyStates.TryGetValue(ev.Seat, out var prev) && prev == ev.Ready) return;

            _roster.SetReady(ev.Seat, ev.Ready);
            _roster.TryGetBySeat(ev.Seat, out var peer);
            ReadyChanged?.Invoke(peer, ev.Ready);

            if (_isHost)
                _network.Send(new PeerReadyChangedEvent { Seat = ev.Seat, Ready = ev.Ready });
        }

        void OnMatchStartReceived(MatchStartEvent ev)
        {
            if (State != SessionState.InLobby) return;
            TransitionTo(SessionState.InMatch);
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

            if (_hostConnectHandler != null) _nm.OnClientConnectedCallback -= _hostConnectHandler;
            if (_hostDisconnectHandler != null) _nm.OnClientDisconnectCallback -= _hostDisconnectHandler;
            if (_clientDisconnectHandler != null) _nm.OnClientDisconnectCallback -= _clientDisconnectHandler;
            _hostConnectHandler = null;
            _hostDisconnectHandler = null;
            _clientDisconnectHandler = null;

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

        public static NetworkManagerSetup CreateNetworkManager()
            => Session<PlayerData, PlayerContext>.CreateNetworkManager();
    }
}
