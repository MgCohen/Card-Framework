#nullable enable
using System.Collections.Generic;

namespace VerticalSlice.QueryBench.Stores
{
    // Shared by both indexed store variants. Encapsulates the
    // marker / value index maintenance logic so Pure and WithEvents
    // are guaranteed to behave identically on the index side —
    // the only difference between them is who calls these methods.
    public sealed class TagIndex
    {
        // slot -> ids whose marker is currently true
        private readonly Dictionary<int, HashSet<int>> _marker = new();
        // slot -> value -> ids currently holding that value
        private readonly Dictionary<int, Dictionary<object, HashSet<int>>> _byValue = new();

        public void RegisterMarker(int slot)
            => _marker[slot] = new HashSet<int>();

        public void RegisterValue(int slot)
            => _byValue[slot] = new Dictionary<object, HashSet<int>>();

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
