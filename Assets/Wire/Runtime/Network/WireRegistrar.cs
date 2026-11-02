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
        static readonly Type[] ValueTypes = { typeof(int), typeof(bool) };

        // TryRegister skips a type that is already registered, for example a block type from AddBlocks.
        public static void RegisterAll(Network network)
        {
            var all = LoadedTypes().ToList();

            foreach (var type in all
                         .Where(IsConcreteWireEvent)
                         .OrderBy(t => t.FullName, StringComparer.Ordinal))
            {
                network.TryRegister(type);
            }

            // Reordering ValueTypes shifts every channel id after it (invariant 1).
            foreach (var def in all
                         .Where(IsOpenGenericWireEvent)
                         .OrderBy(t => t.FullName, StringComparer.Ordinal))
            {
                foreach (var value in ValueTypes)
                {
                    network.TryRegister(def.MakeGenericType(value));
                }
            }
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
