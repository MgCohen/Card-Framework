using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Framework.Wire
{
    // ─────────────────────────────────────────────────────────────────────────────
    // TEMPORARY: reflection-based channel discovery.
    //
    // This is a placeholder until a source generator emits the registration list at
    // compile time. Until that lands, the following invariants MUST hold — they are
    // load-bearing for cross-peer wire compatibility:
    //
    //   1. Deterministic order — channel ids are positional (Network._nextId++), so
    //      both ends must register the SAME types in the SAME order. We sort by
    //      FullName (ordinal) to guarantee that.
    //   2. Symmetric type set — server and client must load the same WireEvent types.
    //      Any type present on one side only shifts every id after it and desyncs.
    //   3. Every WireEvent is networked, and every one must be [AutoPack]-ed (expose a
    //      nested `Packed`). A WireEvent without one is a programming error and throws.
    //
    // Open generics (e.g. TagSetEvent<T>) can't be discovered as closed instantiations,
    // so they're closed over an explicit value-type list — likewise temporary.
    // ─────────────────────────────────────────────────────────────────────────────
    public static class WireRegistrar
    {
        // The set of value types used to close open-generic wire events. Temporary —
        // the source generator (or a game-supplied declaration) will replace this.
        static readonly Type[] ValueTypes = { typeof(int), typeof(bool) };

        static readonly MethodInfo RegisterMethod = typeof(Network)
            .GetMethod(nameof(Network.Register), BindingFlags.Public | BindingFlags.Instance);

        public static void RegisterAll(Network network)
        {
            var all = LoadedTypes().ToList();

            // Pass 1 — concrete, non-generic events (EntityCreatedEvent, TagRemovedEvent,
            // StatePushEvent, Block*Event, game-defined requests/responses, …).
            foreach (var type in all
                         .Where(IsConcreteWireEvent)
                         .OrderBy(t => t.FullName, StringComparer.Ordinal))
            {
                Register(network, type, NestedPacked(type));
            }

            // Pass 2 — open-generic events (TagSetEvent<>, TagChangeEvent<>) closed over
            // each declared value type. Definitions sorted, value types in list order.
            foreach (var def in all
                         .Where(IsOpenGenericWireEvent)
                         .OrderBy(t => t.FullName, StringComparer.Ordinal))
            {
                var packedDef = NestedPacked(def);
                foreach (var value in ValueTypes)
                {
                    Register(network, def.MakeGenericType(value), packedDef.MakeGenericType(value));
                }
            }
        }

        static void Register(Network network, Type eventType, Type packedType)
            => RegisterMethod.MakeGenericMethod(eventType, packedType).Invoke(network, null);

        static Type NestedPacked(Type wireEventType)
        {
            var packed = wireEventType.GetNestedType("Packed");
            if (packed == null)
                throw new InvalidOperationException(
                    $"WireEvent '{wireEventType.FullName}' has no nested 'Packed' type. " +
                    "Every networked WireEvent must be [AutoPack]-ed.");
            return packed;
        }

        static bool IsConcreteWireEvent(Type t)
            => typeof(WireEvent).IsAssignableFrom(t) && !t.IsAbstract && !t.IsGenericTypeDefinition;

        // Open generic definitions aren't assignable to WireEvent via IsAssignableFrom,
        // so walk the base chain instead.
        static bool IsOpenGenericWireEvent(Type t)
        {
            if (!t.IsGenericTypeDefinition || t.IsAbstract) return false;
            for (var b = t.BaseType; b != null; b = b.BaseType)
                if (b == typeof(WireEvent)) return true;
            return false;
        }

        static IEnumerable<Type> LoadedTypes()
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try { types = assembly.GetTypes(); }
                catch (ReflectionTypeLoadException ex) { types = ex.Types.Where(t => t != null).ToArray(); }
                foreach (var t in types) yield return t;
            }
        }
    }
}
