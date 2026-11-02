namespace Framework.Kinds
{
    // The root of every kind space. The slot is private: only the nested Registry reads it.
    public abstract partial record TypeKey : IKind
    {
        protected TypeKey(int slot) => _slot = slot;

        private readonly int _slot;
    }
}
