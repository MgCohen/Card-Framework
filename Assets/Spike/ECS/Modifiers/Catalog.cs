#nullable enable
using System;
using System.Collections.Generic;

namespace Spike.Modifiers
{
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

    public sealed class Catalogs
    {
        public CatalogOf<CardDefinition>     Cards     { get; } = new();
        public CatalogOf<ModifierDefinition> Modifiers { get; } = new();
    }
}
