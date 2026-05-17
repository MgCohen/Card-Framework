#nullable enable
using System.Collections.Generic;
using System.Linq;
using VerticalSlice.QueryBench.Model;

namespace VerticalSlice.QueryBench.Stores
{
    // Mirrors the current World shape: public dict + LINQ queries.
    // No index, no event subscription. Full-table scan on every read.
    public sealed class LinqStore : IBenchStore
    {
        private readonly Dictionary<int, BenchEntity> _entities = new();
        private int _nextId = 1;

        public IReadOnlyDictionary<int, BenchEntity> Entities => _entities;

        public BenchEntity Create()
        {
            var e = new BenchEntity { Id = _nextId++ };
            _entities[e.Id] = e;
            return e;
        }

        public void Set<T>(BenchEntity e, BenchTagKey<T> key, T value) => e.Set(key, value);

        public void Destroy(BenchEntity e) => _entities.Remove(e.Id);

        public BenchEntity? Get(int id) => _entities.TryGetValue(id, out var e) ? e : null;

        // Single marker predicate — current World.Where(TagKey<bool>) shape.
        public IEnumerable<BenchEntity> Where(BenchTagKey<bool> marker)
            => _entities.Values.Where(e => e.Has(marker) && e.Get(marker));

        // Compound — what real call sites do today: stack .Where chains.
        public IEnumerable<BenchEntity> WhereMinionsOf(PlayerId owner)
            => _entities.Values.Where(e =>
                e.Has(BenchTags.IsMinion) && e.Get(BenchTags.IsMinion)
                && e.Has(BenchTags.Owner) && e.Get(BenchTags.Owner) == owner);

        public IEnumerable<BenchEntity> WhereMinionsOnBoardOf(PlayerId owner)
            => _entities.Values.Where(e =>
                e.Has(BenchTags.IsMinion) && e.Get(BenchTags.IsMinion)
                && e.Has(BenchTags.Owner) && e.Get(BenchTags.Owner) == owner
                && e.Has(BenchTags.Zone) && e.Get(BenchTags.Zone) == ZoneId.Board);
    }
}
