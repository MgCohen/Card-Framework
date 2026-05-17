#nullable enable

namespace Spike.EntitySetup
{
    /// <summary>
    /// Minimal engine. Owns the entity store, holds the catalogs
    /// injected by the caller.
    ///
    /// <para>Catalogs in, engine out: content lifetime (catalogs) and
    /// match lifetime (engine + store) are separate. Reset rebuilds
    /// the engine against the same catalogs instance; the catalog is
    /// loaded once at app start.</para>
    /// </summary>
    public sealed class GameEngine
    {
        public Catalogs    Catalogs    { get; }
        public EntityStore EntityStore { get; }

        public GameEngine(Catalogs catalogs)
        {
            Catalogs    = catalogs;
            EntityStore = new EntityStore();
        }
    }

    /// <summary>
    /// Layer 2 — generic def-driven helper. Writes id + Type +
    /// DefinitionId from any <see cref="IEntityDefinition"/>.
    ///
    /// <para>Today it's plumbing-only (just calls
    /// <c>EntityStore.Create(def.Type, def.Id)</c>). It earns its keep
    /// once <see cref="IEntityDefinition"/> gains base parameters that
    /// the L3 type-specific helpers shouldn't have to write themselves.
    /// If that never happens, this layer collapses into the L3
    /// helpers directly.</para>
    /// </summary>
    public static class SpawnExtensions
    {
        public static Entity From(this GameEngine engine, IEntityDefinition def)
            => engine.EntityStore.Create(def.Type, def.Id);
    }
}
