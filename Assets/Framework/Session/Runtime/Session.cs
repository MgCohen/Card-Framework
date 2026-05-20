using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Framework.Wire;

namespace Framework.Session
{
    public sealed class Session : IDisposable
    {
        readonly List<Peer> _peers = new();
        bool _disposed;

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

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            if (State != SessionState.Disconnected)
            {
                State = SessionState.Disconnected;
                Disconnected?.Invoke(DisconnectReason.LocalShutdown);
            }
        }
    }
}
