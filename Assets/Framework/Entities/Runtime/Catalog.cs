using System;
using System.Collections.Generic;

namespace Framework.Entities
{
    public sealed class Catalog<TDef> where TDef : IEntityDefinition
    {
        readonly Dictionary<int, TDef> _byId = new();

        internal void Add(TDef def)
        {
            if (def.Id == 0)
                throw new InvalidOperationException("Definition Id must be non-zero; 0 is the defless sentinel.");
            if (_byId.ContainsKey(def.Id))
                throw new InvalidOperationException($"Id {def.Id} claimed.");
            _byId[def.Id] = def;
        }

        public TDef ById(int id) => _byId[id];

        public bool TryGetById(int id, out TDef def) => _byId.TryGetValue(id, out def);

        public int GetId(TDef def)
        {
            if (!_byId.ContainsKey(def.Id))
                throw new KeyNotFoundException($"No definition with id {def.Id}.");
            return def.Id;
        }
    }
}
