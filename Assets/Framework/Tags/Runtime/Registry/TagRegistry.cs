using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Framework.Tags
{
    /// <summary>
    /// Slot-allocation registry for tags. Holds the authoritative <see cref="TagKey"/>
    /// per int slot and per <see cref="ITagKey"/>-implementing class.
    /// </summary>
    /// <remarks>
    /// <para><b>Registration paths</b></para>
    /// <list type="bullet">
    ///   <item><b>Pattern A</b> — static <c>TagKey&lt;T&gt;</c> instance with explicit slot.
    ///   Base ctor calls <see cref="RegisterInstance"/> on first touch.</item>
    ///   <item><b>Pattern B</b> — <c>[TagSlot(N)] class : ITagKey&lt;T&gt;</c> with
    ///   <see cref="Tag{TClass}.Key"/> (or <c>Tag&lt;TClass, T&gt;.Key</c>) accessor.
    ///   <see cref="For{TClass}"/> synthesizes the <c>TagKey&lt;T&gt;</c> singleton on
    ///   first touch via reflection.</item>
    /// </list>
    /// <para><b>Slot-range conventions</b></para>
    /// <para>Reserved by convention only — not enforced by the registry. Following them
    /// keeps the two patterns from accidentally colliding and makes hand-allocated slots
    /// readable at a glance.</para>
    /// <list type="table">
    ///   <listheader><term>Range</term><description>Use</description></listheader>
    ///   <item><term>1–19</term><description>Kind markers (<c>IsCard</c>, <c>IsMinion</c>, <c>IsZone</c>, …)</description></item>
    ///   <item><term>20–39</term><description>Stats (<c>Health</c>, <c>Attack</c>, <c>Cost</c>, …)</description></item>
    ///   <item><term>40–59</term><description>Plain ability flags (<c>Backlash</c>, <c>DivineShield</c>, …)</description></item>
    ///   <item><term>60–79</term><description>Names (strings — prefer content-id ints when possible)</description></item>
    ///   <item><term>80–99</term><description>Entity-id refs (<c>Zone</c>, <c>Owner</c>, …)</description></item>
    ///   <item><term>100–199</term><description>Pattern B boolean keywords (<c>Deathrattle</c>, …)</description></item>
    ///   <item><term>200+</term><description>Reserved for future Pattern B with non-bool values</description></item>
    /// </list>
    /// <para><b>Slot uniqueness</b> is enforced at registration time — colliding slots
    /// throw <see cref="InvalidOperationException"/>. A compile-time analyzer is deferred
    /// pending a source-gen pass.</para>
    /// </remarks>
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
