#nullable enable
using System;
using System.Collections.Generic;

namespace Spike.EntitySetup
{
    /// <summary>
    /// Typed dict for one definition kind. Dedupes on insert, errors on
    /// duplicate id — content invariants caught at load time, not at
    /// spawn time. Pure data; no loading, no IO, no asset awareness.
    /// </summary>
    public sealed class CatalogOf<TDef> where TDef : IEntityDefinition
    {
        private readonly Dictionary<int, TDef> _byId = new();

        public TDef this[int id] => _byId[id];
        public bool TryGet(int id, out TDef def) => _byId.TryGetValue(id, out def!);
        public IEnumerable<TDef> All => _byId.Values;
        public int Count => _byId.Count;

        public void Add(TDef def)
        {
            if (_byId.ContainsKey(def.Id))
                throw new InvalidOperationException(
                    $"Duplicate definition id {def.Id} for {typeof(TDef).Name}");
            _byId[def.Id] = def;
        }
    }

    /// <summary>
    /// Per-type catalog bag. Card #1 and Zone #1 don't collide because
    /// each type has its own dict. New types add new catalogs:
    /// <c>Catalogs.Modifiers</c>, <c>Catalogs.Tokens</c>, etc.
    ///
    /// <para>Lifetime: content. Lives outside the engine, injected in
    /// via the engine constructor so reset rebuilds match state without
    /// reloading content.</para>
    /// </summary>
    public sealed class Catalogs
    {
        public CatalogOf<CardDefinition> Cards { get; } = new();
        // Zones, Players, Modifiers — added when defs land for them.
    }
}
