namespace Framework.Kinds.Tests
{
    // Each bad shape has its own root, so the Color scan does not find it.
    public abstract record ClashKind : TypeKey { protected ClashKind(int slot) : base(slot) { } }
    public sealed record ClashA() : ClashKind(1);
    public sealed record ClashB() : ClashKind(1);
    public sealed class ClashRegistry : TypeKey.Registry<ClashKind> { }

    public abstract record OpenKind : TypeKey { protected OpenKind(int slot) : base(slot) { } }
    public sealed record OpenMember : OpenKind { public OpenMember(int slot) : base(slot) { } }

    public abstract record PositionalKind(int Slot) : TypeKey(Slot);
}
