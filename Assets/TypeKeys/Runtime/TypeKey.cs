namespace Framework.TypeKeys
{
    public abstract partial record TypeKey : ITypeKey
    {
        protected TypeKey(int slot) => _slot = slot;

        private readonly int _slot;
    }
}
