#nullable enable
using VerticalSlice.QueryBench.Model;

namespace VerticalSlice.QueryBench.Stores
{
    // Uniform mutation surface so PopulationBuilder targets every store
    // through the same calls. Lets us hold the population-build path
    // constant while varying the implementation under test.
    public interface IBenchStore
    {
        BenchEntity Create();
        void Set<T>(BenchEntity e, BenchTagKey<T> key, T value);
        void Destroy(BenchEntity e);
        BenchEntity? Get(int id);
    }
}
