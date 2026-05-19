using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Framework.Tags
{
    public static class TagRegistry
    {
        static readonly Dictionary<int, TagKey> _bySlot = new();
        static readonly Dictionary<Type, TagKey> _byClass = new();

        public static void RegisterInstance(TagKey key)
        {
            if (_bySlot.TryGetValue(key.Slot, out var existing))
            {
                if (ReferenceEquals(existing, key)) return;
                throw new InvalidOperationException(
                    $"Slot {key.Slot} double-claimed (existing: {existing}, new: {key})");
            }
            _bySlot[key.Slot] = key;
        }

        public static TagKey For<TClass>() where TClass : ITagKey => For(typeof(TClass));

        public static TagKey For(Type tagClass)
        {
            if (_byClass.TryGetValue(tagClass, out var existing)) return existing;

            var attr = tagClass.GetCustomAttribute<TagSlotAttribute>()
                ?? throw new InvalidOperationException(
                    $"{tagClass.Name} missing [TagSlot]");

            var iface = tagClass.GetInterfaces()
                .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ITagKey<>))
                ?? throw new InvalidOperationException(
                    $"{tagClass.Name} must implement ITagKey<T>");

            var valueType = iface.GetGenericArguments()[0];
            var keyType = typeof(TagKey<>).MakeGenericType(valueType);
            var key = (TagKey)Activator.CreateInstance(keyType, attr.Slot);

            _byClass[tagClass] = key;
            return key;
        }

        public static TagKey BySlot(int slot) => _bySlot[slot];

        public static bool TryGetBySlot(int slot, out TagKey key) => _bySlot.TryGetValue(slot, out key);

        public static IReadOnlyCollection<TagKey> All => _bySlot.Values;
    }
}
