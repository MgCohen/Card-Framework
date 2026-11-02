namespace Framework.Kinds
{
    public abstract partial record TypeKey : IKind
    {
        protected TypeKey(int slot) => _slot = slot;

        private readonly int _slot;
    }
}
