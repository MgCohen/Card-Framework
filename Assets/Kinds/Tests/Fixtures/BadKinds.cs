using System;

namespace Framework.Kinds.Tests
{
    // Each bad shape has its own root, so the Color scan does not find it.
    public abstract record ClashKind : TypeKey { protected ClashKind(int slot) : base(slot) { } }
    public sealed record ClashA() : ClashKind(1);
    public sealed record ClashB() : ClashKind(1);

    public abstract record OpenKind : TypeKey { protected OpenKind(int slot) : base(slot) { } }
    public sealed record OpenMember : OpenKind { public OpenMember(int slot) : base(slot) { } }

    public abstract record PositionalKind(int Slot) : TypeKey(Slot);

    public abstract record ThrowKind : TypeKey { protected ThrowKind(int slot) : base(slot) { } }
    public sealed record ThrowMember : ThrowKind { public ThrowMember() : base(1) => throw new InvalidOperationException("ThrowMember constructor failed."); }
}
