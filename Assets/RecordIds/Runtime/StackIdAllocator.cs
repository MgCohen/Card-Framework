namespace Framework.RecordIds
{
    public sealed class StackIdAllocator<TId> : IdAllocator<TId> where TId : Id, new()
    {
        public StackIdAllocator(int first = 1) : base(first) { }

        // Only the last id goes back; releasing any other id leaves a gap.
        public override void Release(TId id)
        {
            if (id != null && HasGiven && id.Value == Last) StepBack();
        }
    }
}
