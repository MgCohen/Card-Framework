namespace Framework.RecordIds
{
    public class IdAllocator<TId> where TId : Id, new()
    {
        readonly int _first;
        int _next;

        public IdAllocator(int first = 1)
        {
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
