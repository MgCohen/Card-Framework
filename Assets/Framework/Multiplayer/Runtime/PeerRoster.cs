using System.Collections.Generic;

namespace Framework.Multiplayer
{
    public sealed class PeerRoster<TContext> where TContext : PlayerContext
    {
        readonly List<Peer> _peers = new();
        readonly Dictionary<int, TContext> _players = new();
        readonly Dictionary<int, bool> _ready = new();
        int _nextSeat = 1;

        public IReadOnlyList<Peer> Peers => _peers;
        public IReadOnlyDictionary<int, TContext> Players => _players;
        public IReadOnlyDictionary<int, bool> ReadyStates => _ready;

        public bool AllReady
        {
            get
            {
                if (_peers.Count == 0) return false;
                foreach (var peer in _peers)
                    if (!_ready.TryGetValue(peer.Seat, out var r) || !r) return false;
                return true;
            }
        }

        public int NextSeat() => _nextSeat++;

        public void Add(Peer peer) => _peers.Add(peer);

        public void SetContext(int seat, TContext context) => _players[seat] = context;

        public void SetReady(int seat, bool ready) => _ready[seat] = ready;

        public bool TryGetBySeat(int seat, out Peer peer)
        {
            foreach (var p in _peers)
                if (p.Seat == seat) { peer = p; return true; }
            peer = default;
            return false;
        }

        public bool TryGetByNetworkId(ulong networkId, out Peer peer)
        {
            foreach (var p in _peers)
                if (p.NetworkId == networkId) { peer = p; return true; }
            peer = default;
            return false;
        }

        public bool Remove(int seat, out Peer removed)
        {
            for (var i = 0; i < _peers.Count; i++)
            {
                if (_peers[i].Seat != seat) continue;
                removed = _peers[i];
                _peers.RemoveAt(i);
                _players.Remove(seat);
                _ready.Remove(seat);
                return true;
            }
            removed = default;
            return false;
        }
    }
}
