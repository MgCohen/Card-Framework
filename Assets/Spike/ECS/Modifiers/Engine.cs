#nullable enable

namespace Spike.Modifiers
{
    public sealed class GameEngine
    {
        public Catalogs    Catalogs    { get; }
        public EntityStore EntityStore { get; }
        public Effects     Effects     { get; }

        public GameEngine(Catalogs catalogs)
        {
            Catalogs    = catalogs;
            EntityStore = new EntityStore();
            Effects     = new Effects();
            TagPolicies.RegisterDefaults();
        }
    }

    // Effect-scope factory. Sandbox-grade: every NewScope is a fresh
    // empty marker. Real implementation would carry causality / source /
    // wire batch — punted with IEffectScope shape.
    public sealed class Effects
    {
        public IEffectScope NewScope() => new EffectScope();
    }

    // Layer 2 — generic def-driven helper.
    public static class SpawnExtensions
    {
        public static Entity From(this GameEngine engine, IEntityDefinition def)
            => engine.EntityStore.Create(def.Type, def.Id);
    }
}
