using System.Collections;
using NUnit.Framework;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.TestTools;

namespace Framework.Wire.PlayTests
{
    /// <summary>
    /// Play-mode validation for <see cref="NgoTransport"/>. Spins up a real
    /// <see cref="NetworkManager"/> in host mode with <see cref="UnityTransport"/>,
    /// builds a wire layer on top, and exercises a round-trip through NGO's
    /// <c>CustomMessagingManager</c>. Host-mode broadcast loops back to the host's
    /// own handler — proves the adapter wires up correctly without requiring two
    /// distinct NetworkManager instances.
    /// </summary>
    public class NgoTransportPlayTests
    {
        const string ListenAddress = "127.0.0.1";
        const ushort Port = 7787;

        GameObject _nmGo;
        NetworkManager _nm;
        NgoTransport _transport;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            _transport?.Dispose();
            _transport = null;

            if (_nm != null && _nm.IsListening)
            {
                _nm.Shutdown();
                yield return null;
            }

            if (_nmGo != null)
            {
                Object.DestroyImmediate(_nmGo);
                _nmGo = null;
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator Host_Broadcast_RoundTrips_Through_NgoTransport()
        {
            _nmGo = new GameObject("NetworkManager");
            _nm = _nmGo.AddComponent<NetworkManager>();

            var utp = _nmGo.AddComponent<UnityTransport>();
            utp.SetConnectionData(ListenAddress, Port, ListenAddress);

            _nm.NetworkConfig = new NetworkConfig
            {
                NetworkTransport = utp,
                TickRate = 30,
            };

            Assert.IsTrue(_nm.StartHost(), "StartHost should succeed");
            yield return null;

            _transport = new NgoTransport(_nm); // broadcast — host loops back to its own handler
            var network = new Network(_transport);
            var channel = network.Register<EntityCreatedEvent>();

            EntityCreatedEvent received = null;
            channel.OnReceived += ev => received = ev;

            network.Send(new EntityCreatedEvent { EntityId = 42 });

            // NGO dispatches custom messages on the next tick — yield a couple frames.
            yield return null;
            yield return null;

            Assert.IsNotNull(received, "Channel should have received the broadcast event");
            Assert.AreEqual(42, received.EntityId);
        }
    }
}
