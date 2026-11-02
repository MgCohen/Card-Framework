using System;

namespace Framework.RecordIds
{
    public class IdAllocator<TId> where TId : Id, new()
    {
        readonly int _first;
        int _next;

        public IdAllocator(int first = 1)
        {
            // -1 is "no id" and 0 is "never set", so a real id is 1 or more.
            if (first < 1) throw new ArgumentOutOfRangeException(nameof(first), first, "The first id must be 1 or more.");
            _first = first;
            _next = first;
        }

        public TId Next() => new TId { Value = _next++ };

        // Deliberate no-op: the base never gives an id back.
        public virtual void Release(TId id) { }

        protected bool HasGiven => _next > _first;

        protected int Last => _next - 1;

        protected void StepBack() => _next--;
    }
}
