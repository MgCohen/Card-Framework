using System.Collections.Generic;

namespace Framework.RecordIds
{
    public abstract class RecordStore<TRecord, TId>
        where TRecord : IRecord<TId>
        where TId : Id
    {
        readonly Dictionary<TId, TRecord> _byId = new();

        public int Count => _byId.Count;

        public TRecord ById(TId id) =>
            _byId.TryGetValue(id, out var record)
                ? record
                : throw new KeyNotFoundException($"No {typeof(TRecord).Name} with {id}.");

        public bool TryGetById(TId id, out TRecord record) => _byId.TryGetValue(id, out record);

        protected void Write(TRecord record) => _byId[record.Id] = record;

        protected bool Erase(TId id) => _byId.Remove(id);
    }
}
