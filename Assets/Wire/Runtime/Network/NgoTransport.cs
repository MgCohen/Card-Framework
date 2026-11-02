using System;
using Unity.Collections;
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

        public event TransportReceive OnReceived;

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

        public unsafe void Send(ReadOnlySpan<byte> bytes)
        {
            var writer = new FastBufferWriter(bytes.Length, Allocator.Temp);
            try
            {
                fixed (byte* p = bytes) writer.WriteBytesSafe(p, bytes.Length);
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
            finally
            {
                writer.Dispose();
            }
        }

        // NGO hands over the reader positioned after the named-message header.
        unsafe void OnNgoMessageReceived(ulong senderClientId, FastBufferReader payload)
            => OnReceived?.Invoke(new ReadOnlySpan<byte>(
                   payload.GetUnsafePtrAtCurrentPosition(), payload.Length - payload.Position));

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
