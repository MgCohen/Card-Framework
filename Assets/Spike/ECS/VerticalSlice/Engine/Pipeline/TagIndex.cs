#nullable enable
using System.Collections.Generic;

namespace VerticalSlice.Engine.Pipeline
{
    /// <summary>
    /// Per-tag inverted indices: slot → set of entity ids carrying that
    /// tag (markers) or slot → value → set of ids (value indices). Owned
    /// by <see cref="EntityStore"/>; maintained on every tag write.
    ///
    /// <para>Two index kinds:
    /// <list type="bullet">
    /// <item><b>Marker</b> — for <c>TagKey&lt;bool&gt;</c>. The set holds
    /// the ids whose tag value is currently <c>true</c>. Setting to
    /// <c>false</c> removes the id from the set.</item>
    /// <item><b>Value</b> — for any tag. The dictionary buckets ids by
    /// current value (e.g. <c>Zone → ids in that zone</c>). On every
    /// value change the id moves between buckets.</item>
    /// </list>
    /// </para>
    ///
    /// <para>Indices are opt-in via <see cref="EntityStore.IndexMarker"/>
    /// / <see cref="EntityStore.IndexBy{T}"/>. Tags without registered
    /// indices still work for Get/Set — they just don't get a query
    /// path; full-table-style traversal is unavailable by design.</para>
    /// </summary>
    public sealed class TagIndex
    {
        private readonly Dictionary<int, HashSet<int>> _marker = new();
        private readonly Dictionary<int, Dictionary<object, HashSet<int>>> _byValue = new();

        public void RegisterMarker(int slot) => _marker[slot] = new HashSet<int>();
        public void RegisterValue(int slot)  => _byValue[slot] = new Dictionary<object, HashSet<int>>();

        public bool TryGetMarker(int slot, out HashSet<int> set)
            => _marker.TryGetValue(slot, out set!);

        public bool TryGetValueBucket(int slot, object value, out HashSet<int> set)
        {
            if (_byValue.TryGetValue(slot, out var byValue)
                && byValue.TryGetValue(value, out set!))
                return true;
            set = EmptySet;
            return false;
        }

        /// <summary>Shared empty set returned for unregistered or absent buckets.
        /// The query enumerator walks zero items against this and yields nothing.</summary>
        public static readonly HashSet<int> EmptySet = new();

        public void OnTagSet(int entityId, int slot, object value)
        {
            if (_marker.TryGetValue(slot, out var mset) && value is bool b && b)
                mset.Add(entityId);
            if (_byValue.TryGetValue(slot, out var byValue))
                Bucket(byValue, value).Add(entityId);
        }

        public void OnTagChanged(int entityId, int slot, object prev, object next)
        {
            if (_marker.TryGetValue(slot, out var mset) && next is bool b)
            {
                if (b) mset.Add(entityId); else mset.Remove(entityId);
            }
            if (_byValue.TryGetValue(slot, out var byValue))
            {
                if (byValue.TryGetValue(prev, out var oldSet)) oldSet.Remove(entityId);
                Bucket(byValue, next).Add(entityId);
            }
        }

        public void OnTagRemoved(int entityId, int slot, object prev)
        {
            if (_marker.TryGetValue(slot, out var mset))
                mset.Remove(entityId);
            if (_byValue.TryGetValue(slot, out var byValue)
                && byValue.TryGetValue(prev, out var vset))
                vset.Remove(entityId);
        }

        public void OnEntityDestroyed(int entityId, IEnumerable<KeyValuePair<int, object?>> slots)
        {
            foreach (var kv in slots)
            {
                if (_marker.TryGetValue(kv.Key, out var mset))
                    mset.Remove(entityId);
                if (kv.Value != null
                    && _byValue.TryGetValue(kv.Key, out var byValue)
                    && byValue.TryGetValue(kv.Value, out var vset))
                    vset.Remove(entityId);
            }
        }

        private static HashSet<int> Bucket(Dictionary<object, HashSet<int>> byValue, object v)
        {
            if (!byValue.TryGetValue(v, out var set))
                byValue[v] = set = new HashSet<int>();
            return set;
        }
    }
}
