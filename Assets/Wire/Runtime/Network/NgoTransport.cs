using System;
using Unity.Netcode;

namespace Framework.Wire
{
    public sealed class NgoTransport : ITransport, IDisposable
    {
        public const string DefaultMessageName = "Framework.Wire";

        readonly NetworkManager _networkManager;
        readonly string _messageName;
        readonly ulong? _targetClientId;
        bool _disposed;

        public event Action<FastBufferReader> OnReceived;

        public NgoTransport(
            NetworkManager networkManager,
            string messageName = DefaultMessageName,
            ulong? targetClientId = null)
        {
            _networkManager = networkManager;
            _messageName = messageName;
            _targetClientId = targetClientId;
            _networkManager.CustomMessagingManager.RegisterNamedMessageHandler(_messageName, OnNgoMessageReceived);
        }

        public void Send(in FastBufferWriter writer)
        {
            if (_targetClientId.HasValue)
            {
                _networkManager.CustomMessagingManager.SendNamedMessage(
                    _messageName, _targetClientId.Value, writer);
            }
            else
            {
                _networkManager.CustomMessagingManager.SendNamedMessageToAll(_messageName, writer);
            }
        }

        void OnNgoMessageReceived(ulong senderClientId, FastBufferReader payload)
            => OnReceived?.Invoke(payload);

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            if (_networkManager != null && _networkManager.CustomMessagingManager != null)
            {
                _networkManager.CustomMessagingManager.UnregisterNamedMessageHandler(_messageName);
            }
        }
    }
}
