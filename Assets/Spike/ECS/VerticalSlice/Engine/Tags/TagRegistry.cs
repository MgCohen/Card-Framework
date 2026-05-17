#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace VerticalSlice.Engine.Tags
{
    /// <summary>
    /// Process-global slot registry. Holds two registration paths into
    /// one slot space:
    ///
    /// <list type="bullet">
    /// <item><b>Pattern A — instance registration.</b> Called from the
    /// <see cref="TagKey"/> base ctor; claims the explicit slot and
    /// stores the typed key.</item>
    /// <item><b>Pattern B — type registration.</b> Called explicitly for
    /// keyword classes carrying <c>[TagSlot]</c>; claims the slot from
    /// the attribute and synthesizes a hidden <see cref="TagKey{T}"/>
    /// singleton at it. Look up via <see cref="Tag{TTag}.Key"/> or
    /// <see cref="GetSynthesized{TTag}"/>.</item>
    /// </list>
    ///
    /// <para>Both paths share one slot space — collisions throw with the
    /// offending owner. Slots are author-assigned at declaration; this
    /// registry just claims + indexes them.</para>
    ///
    /// <para>Process-global for now (slice scope). Production should
    /// scope per-World so app-domain reloads don't drift slot indices.</para>
    /// </summary>
    public static class TagRegistry
    {
        private static readonly Dictionary<int, TagKey> _bySlot = new();
        private static readonly HashSet<int> _claimed = new();
        private static readonly Dictionary<Type, TagKey> _synthesized = new();

        public static int Count => _claimed.Count;
        public static IEnumerable<TagKey> All => _bySlot.Values;

        // --- Pattern A — instance registration ---------------------------

        internal static void RegisterInstance(TagKey key)
        {
            Claim(key.Slot, key.GetType());
            _bySlot[key.Slot] = key;
        }

        // --- Pattern B — type registration -------------------------------

        /// <summary>
        /// Register a Pattern B tag class. Reads the class's <c>[TagSlot]</c>
        /// attribute + <see cref="ITagKey{T}"/> generic argument, claims
        /// the slot, and synthesizes a <c>TagKey&lt;T&gt;</c> at it.
        /// Returns the synthesized key. Idempotent — re-registering the
        /// same class returns the existing key.
        /// </summary>
        public static TagKey RegisterType<TTag>() where TTag : class
            => RegisterType(typeof(TTag));

        public static TagKey RegisterType(Type tagClass)
        {
            if (_synthesized.TryGetValue(tagClass, out var existing)) return existing;

            var attr = (TagSlotAttribute?)Attribute.GetCustomAttribute(tagClass, typeof(TagSlotAttribute))
                ?? throw new InvalidOperationException(
                    $"{tagClass.Name} is missing [TagSlot] — Pattern B classes must declare their slot.");
            var iface = tagClass.GetInterfaces().FirstOrDefault(
                i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ITagKey<>))
                ?? throw new InvalidOperationException(
                    $"{tagClass.Name} doesn't implement ITagKey<> — Pattern B classes must declare a value type via the marker interface.");
            var valueType = iface.GetGenericArguments()[0];

            var keyType = typeof(TagKey<>).MakeGenericType(valueType);
            var debugName = attr.DebugName ?? tagClass.Name;
            var synth = (TagKey)Activator.CreateInstance(keyType, attr.Slot, debugName)!;
            _synthesized[tagClass] = synth;
            return synth;
        }

        /// <summary>
        /// Resolve the synthesized <see cref="TagKey"/> for a Pattern B
        /// class. Lazy-registers if not already registered. Used by
        /// <see cref="Tag{TTag}.Key"/>.
        /// </summary>
        public static TagKey GetSynthesized<TTag>() where TTag : class
            => _synthesized.TryGetValue(typeof(TTag), out var k) ? k : RegisterType(typeof(TTag));

        public static TagKey? GetBySlot(int slot)
            => _bySlot.TryGetValue(slot, out var k) ? k : null;

        // --- Slot claim --------------------------------------------------

        private static void Claim(int slot, Type owner)
        {
            if (!_claimed.Add(slot))
            {
                var prior = _bySlot.TryGetValue(slot, out var p) ? p.GetType().Name : "<unknown>";
                throw new InvalidOperationException(
                    $"Tag slot {slot} double-claimed: {owner.Name} collides with {prior}. Pick a different slot.");
            }
        }
    }

    /// <summary>
    /// Generic-static accessor for Pattern B keys. JIT specializes once
    /// per <typeparamref name="TTag"/>; subsequent access is a direct
    /// field read. Bootstrap can pre-warm a Pattern B class by touching
    /// <c>Tag&lt;Foo&gt;.Key</c> (or implicitly via
    /// <c>store.IndexMarker(Tag&lt;Foo&gt;.Key as TagKey&lt;bool&gt;)</c>).
    /// </summary>
    public static class Tag<TTag> where TTag : class
    {
        public static TagKey Key { get; } = TagRegistry.GetSynthesized<TTag>();
    }
}
