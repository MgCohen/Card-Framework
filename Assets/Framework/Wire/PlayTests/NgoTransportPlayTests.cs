using System.Collections;
using NUnit.Framework;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.TestTools;

namespace Framework.Wire.PlayTests
{
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

            _transport = new NgoTransport(_nm);
            var network = new Network(_transport);
            var channel = network.Register<EntityCreatedEvent, EntityCreatedEvent.Packed>();

            EntityCreatedEvent received = null;
            channel.OnReceived += ev => received = ev;

            network.Send(new EntityCreatedEvent { EntityId = 42 });

            yield return null;
            yield return null;

            Assert.IsNotNull(received, "Channel should have received the broadcast event");
            Assert.AreEqual(42, received.EntityId);
        }
    }
}
