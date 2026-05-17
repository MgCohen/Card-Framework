#nullable enable
using System;
using System.Collections.Generic;

namespace StateMachine.Engine
{
    /// <summary>
    /// Maps GameState record types to a 1-byte wire kind. The wire StatePushEvent
    /// carries the byte; apply walks back through Resolve() to a record instance.
    /// Game-authored kinds start at 10 (framework reserves 0–9).
    /// </summary>
    public interface IStateRegistry
    {
        void      Register<T>(byte kind, Func<T> factory) where T : GameState;
        GameState Resolve(byte kind);
        byte      KindOf(GameState state);
        bool      TryKindOf(GameState state, out byte kind);
        IEnumerable<(byte Kind, Type Type)> All { get; }
    }

    public sealed class StateRegistry : IStateRegistry
    {
        private readonly Dictionary<byte, Func<GameState>> _factoryByKind = new();
        private readonly Dictionary<Type, byte>            _kindByType    = new();

        public void Register<T>(byte kind, Func<T> factory) where T : GameState
        {
            if (_factoryByKind.ContainsKey(kind))
                throw new InvalidOperationException($"Kind {kind} already registered.");
            if (_kindByType.ContainsKey(typeof(T)))
                throw new InvalidOperationException($"State {typeof(T).Name} already registered.");
            _factoryByKind[kind] = () => factory();
            _kindByType[typeof(T)] = kind;
        }

        public GameState Resolve(byte kind)
        {
            if (!_factoryByKind.TryGetValue(kind, out var f))
                throw new InvalidOperationException($"No state registered for kind {kind}.");
            return f();
        }

        public byte KindOf(GameState state)
        {
            if (!_kindByType.TryGetValue(state.GetType(), out var kind))
                throw new InvalidOperationException($"State {state.GetType().Name} not registered.");
            return kind;
        }

        public bool TryKindOf(GameState state, out byte kind) =>
            _kindByType.TryGetValue(state.GetType(), out kind);

        public IEnumerable<(byte Kind, Type Type)> All
        {
            get
            {
                foreach (var kv in _kindByType) yield return (kv.Value, kv.Key);
            }
        }
    }
}
