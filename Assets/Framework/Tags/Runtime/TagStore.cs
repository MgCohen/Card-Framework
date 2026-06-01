using System;
using System.Collections.Generic;
using Scaffold.Maps;

namespace Framework.Tags
{
    // Pure repository over (entityId, slot) -> value, stored inline per value type — no boxing.
    // One Map<int,int,T> per value type, created lazily on first Set<T>. No emit, no event
    // awareness. Apply handlers write through here; TagService reads through it.
    //
    // The store is self-contained: it never consults TagRegistry. Set<T>/Get<T> route by T;
    // the non-generic Has/Remove probe the (few) typed maps, since a given (entityId, slot)
    // lives in exactly one map — its slot fixes the value type.
    public sealed class TagStore : IReadOnlyTagStore
    {
        readonly Dictionary<Type, ITagMap> _byType = new();

        internal void Set<T>(int entityId, int slot, T value) where T : unmanaged
            => MapFor<T>().Set(entityId, slot, value);

        public T Get<T>(int entityId, int slot) where T : unmanaged
            => ((TagMap<T>)_byType[typeof(T)]).Get(entityId, slot);

        public bool Has(int entityId, int slot)
        {
            foreach (var map in _byType.Values)
                if (map.Has(entityId, slot)) return true;
            return false;
        }

        internal bool Remove(int entityId, int slot)
        {
            foreach (var map in _byType.Values)
                if (map.Remove(entityId, slot)) return true;
            return false;
        }

        TagMap<T> MapFor<T>() where T : unmanaged
        {
            if (!_byType.TryGetValue(typeof(T), out var map))
            {
                map = new TagMap<T>();
                _byType[typeof(T)] = map;
            }
            return (TagMap<T>)map;
        }

        // Implementation of one store — nested deliberately, not free-floating types.
        interface ITagMap
        {
            bool Has(int entityId, int slot);
            bool Remove(int entityId, int slot);
        }

        sealed class TagMap<T> : ITagMap where T : unmanaged
        {
            readonly Map<int, int, T> _m = new();
            public T    Get(int entityId, int slot)            => _m[entityId, slot];
            public void Set(int entityId, int slot, T value)   => _m[entityId, slot] = value;
            public bool Has(int entityId, int slot)            => _m.Contains(entityId, slot);
            public bool Remove(int entityId, int slot)         => _m.Remove(entityId, slot);
        }
    }
}
