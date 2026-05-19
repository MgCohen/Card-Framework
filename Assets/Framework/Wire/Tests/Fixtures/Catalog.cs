using System.Collections.Generic;

namespace Framework.Wire.Tests
{
    public sealed class Catalog
    {
        readonly Dictionary<int, CardDefinition> _byId = new();

        public void Add(CardDefinition def) => _byId[def.Id] = def;
        public CardDefinition GetById(int id) => _byId.TryGetValue(id, out var d) ? d : null;
        public int GetId(CardDefinition def) => def?.Id ?? 0;
    }
}
