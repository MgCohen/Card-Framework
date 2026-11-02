using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using Framework.Stripping;
using Scaffold.AutoPacker;

namespace Framework.Wire
{
    public sealed class Network
    {
        // Found by shape, not by name alone, so a new Register overload does not break it.
        static readonly MethodInfo RegisterDefinition = typeof(Network)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Single(m => m.Name == nameof(Register) && m.IsGenericMethodDefinition && m.GetGenericArguments().Length == 2);

        readonly Dispatcher _dispatcher;
        readonly IPackingHandler _handler;
        readonly Dictionary<Type, NetworkChannel> _byType = new();
        int _nextId;

        public event Action<WireEvent> OnReceived;

        public Network(ITransport transport, IPackingHandler handler = null)
        {
            _handler = handler;
            _dispatcher = new Dispatcher(transport);
            _dispatcher.OnAnyReceived += ev => OnReceived?.Invoke(ev);
        }

        // Preserve: TryRegister can be the only caller, through reflection.
        [Preserve]
        public NetworkChannel<T> Register<T, TPacked>()
            where T : WireEvent, IPackable<TPacked>, IUnpackable<TPacked>, new()
            where TPacked : unmanaged, IPackedStruct
        {
            if (_byType.ContainsKey(typeof(T)))
                throw new InvalidOperationException($"Wire channel for '{typeof(T).FullName}' is already registered. Use TryRegister to skip it.");
            if (_nextId >= Dispatcher.Capacity)
                throw new InvalidOperationException($"Wire channel registry full: {Dispatcher.Capacity} channels max (byte-sized ids).");
            var id = (byte)_nextId++;
            var ch = new NetworkChannel<T, TPacked>(id, _dispatcher, _handler);
            _byType[typeof(T)] = ch;
            _dispatcher.Register(id, ch);
            return ch;
        }

        public bool IsRegistered(Type eventType) => _byType.ContainsKey(eventType);

        // Registers a concrete WireEvent with its nested Packed struct. Returns false if it is already registered.
        public bool TryRegister(Type eventType)
        {
            if (IsRegistered(eventType)) return false;
            if (!typeof(WireEvent).IsAssignableFrom(eventType) || eventType.IsAbstract || eventType.ContainsGenericParameters)
                throw new ArgumentException($"'{eventType.FullName}' is not a concrete WireEvent.", nameof(eventType));

            try
            {
                RegisterDefinition.MakeGenericMethod(eventType, PackedOf(eventType)).Invoke(this, null);
            }
            catch (TargetInvocationException e) when (e.InnerException != null)
            {
                ExceptionDispatchInfo.Capture(e.InnerException).Throw();
            }
            return true;
        }

        public void Send(WireEvent ev) => _byType[ev.GetType()].SendBoxed(ev);

        static Type PackedOf(Type eventType)
        {
            var packed = eventType.GetNestedType("Packed")
                ?? throw new InvalidOperationException(
                    $"WireEvent '{eventType.FullName}' has no nested 'Packed' type. " +
                    "Every networked WireEvent must be [AutoPack]-ed.");
            // The nested type of a closed generic event is open; close it over the same arguments.
            return packed.IsGenericTypeDefinition ? packed.MakeGenericType(eventType.GetGenericArguments()) : packed;
        }
    }
}
