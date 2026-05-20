using System;
using System.Collections.Generic;

namespace Framework.Entities
{
    public sealed class Catalog<TDef> where TDef : IEntityDefinition
    {
        readonly Dictionary<int, TDef> _byId = new();
        readonly Dictionary<TDef, int> _byDef = new();

        public void Add(TDef def)
        {
            if (_byId.ContainsKey(def.Id))
                throw new InvalidOperationException($"Id {def.Id} claimed.");
            _byId[def.Id] = def;
            _byDef[def]   = def.Id;
        }

        public TDef GetById(int id) => _byId[id];

        public bool TryGetById(int id, out TDef def) => _byId.TryGetValue(id, out def);

        public int GetId(TDef def) => _byDef[def];
    }
}
