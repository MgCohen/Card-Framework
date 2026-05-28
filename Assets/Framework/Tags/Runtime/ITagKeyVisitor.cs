namespace Framework.Tags
{
    // Double-dispatch seam so non-generic callers can drive the closed type.
    // `TagKey<T>.Accept(visitor)` calls `visitor.Visit(this)` and the compiler
    // picks `Visit<T>` statically — no reflection, no MakeGenericType.
    public interface ITagKeyVisitor
    {
        void Visit<T>(TagKey<T> key) where T : unmanaged;
    }
}
