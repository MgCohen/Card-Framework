using System;
using Unity.Netcode;

namespace Framework.Wire
{
    /// <summary>
    /// <see cref="ITransport"/> backed by Unity NGO's <see cref="CustomMessagingManager"/>.
    /// Send maps to <c>SendNamedMessage</c> (targeted) or <c>SendNamedMessageToAll</c> (broadcast);
    /// receive registers a named-message handler that surfaces the payload via
    /// <see cref="ITransport.OnReceived"/>.
    /// </summary>
    /// <remarks>
    /// Construction options:
    /// <list type="bullet">
    ///   <item>Server-side broadcasting to all clients: pass <c>null</c> for <c>targetClientId</c>.</item>
    ///   <item>Client-side sending to the server: pass <see cref="NetworkManager.ServerClientId"/>.</item>
    ///   <item>Server-side targeting one client: pass that client's id.</item>
    /// </list>
    /// One instance per outbound destination. Dispose to release the named-message handler.
    /// </remarks>
    public sealed class NgoTransport : ITransport, IDisposable
    {
        /// <summary>Default named-message identifier used when no override is supplied.</summary>
        public const string DefaultMessageName = "Framework.Wire";

        readonly NetworkManager _networkManager;
        readonly string _messageName;
        readonly ulong? _targetClientId;
        bool _disposed;

        public event TransportReceiveCallback OnReceived;

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
