#nullable enable
using System;
using System.Collections.Generic;

namespace StateMachine.Engine
{
    public abstract class TagKey
    {
        public int    Slot      { get; }
        public string Name      { get; }
        public Type   ValueType { get; }

        protected TagKey(int slot, string name, Type valueType)
        { Slot = slot; Name = name; ValueType = valueType; }

        public override string ToString() => $"Tag({Name}#{Slot}:{ValueType.Name})";
    }

    public sealed class TagKey<T> : TagKey
    {
        internal TagKey(int slot, string name) : base(slot, name, typeof(T)) { }
    }

    public static class TagRegistry
    {
        private static int _next = 1;
        private static readonly Dictionary<string, TagKey> _byName = new();

        public static TagKey<T> Allocate<T>(string name)
        {
            if (_byName.TryGetValue(name, out var existing))
            {
                if (existing is TagKey<T> typed) return typed;
                throw new InvalidOperationException(
                    $"Tag '{name}' already declared with type {existing.ValueType.Name}; cannot redeclare as {typeof(T).Name}.");
            }
            var k = new TagKey<T>(_next++, name);
            _byName[name] = k;
            return k;
        }
    }
}
