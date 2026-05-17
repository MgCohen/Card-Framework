#nullable enable
using System;
using System.Collections.Generic;

namespace TypedPayloads.Engine
{
    /// <summary>
    /// Allocates and tracks <see cref="TagKey{T}"/> declarations across the
    /// whole process. Single shared instance — tag identity must agree
    /// between producer and consumer of the wire.
    ///
    /// <para>Name-stable: declaring the same name twice with the same type
    /// returns the same key. Re-declaring with a different type throws —
    /// catches accidental shadowing at startup rather than producing two
    /// slots that look the same.</para>
    /// </summary>
    public static class TagRegistry
    {
        private static int _next = 1;
        private static readonly Dictionary<int, TagKey>    _bySlot = new();
        private static readonly Dictionary<string, TagKey> _byName = new();

        public static TagKey<T> Allocate<T>(string name)
        {
            if (_byName.TryGetValue(name, out var existing))
            {
                if (existing is TagKey<T> typed) return typed;
                throw new InvalidOperationException(
                    $"Tag '{name}' already declared with type {existing.ValueType.Name}; " +
                    $"cannot redeclare as {typeof(T).Name}.");
            }
            var k = new TagKey<T>(_next++, name);
            _bySlot[k.Slot] = k;
            _byName[name]   = k;
            return k;
        }

        public static TagKey BySlot(int slot) => _bySlot[slot];

        public static IReadOnlyCollection<TagKey> All => _bySlot.Values;
    }
}
