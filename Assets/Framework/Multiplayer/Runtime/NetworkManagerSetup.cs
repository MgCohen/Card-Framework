using Unity.Netcode;
using Unity.Netcode.Transports.UTP;

namespace Framework.Multiplayer
{
    public readonly struct NetworkManagerSetup
    {
        public readonly NetworkManager Manager;
        public readonly UnityTransport Transport;

        public NetworkManagerSetup(NetworkManager manager, UnityTransport transport)
        {
            Manager   = manager;
            Transport = transport;
        }
    }
}
