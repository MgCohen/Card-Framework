using TypeKeys.Domain.Shared;

namespace TypeKeys.Tests;

// Each bad shape has its own root, so the Color scan does not find it.
public abstract record ClashKey : TypeKey { protected ClashKey(int slot) : base(slot) { } }
public sealed record ClashA() : ClashKey(1);
public sealed record ClashB() : ClashKey(1);

public abstract record OpenKey : TypeKey { protected OpenKey(int slot) : base(slot) { } }
public sealed record OpenMember : OpenKey { public OpenMember(int slot) : base(slot) { } }

public abstract record PositionalKey(int Position) : TypeKey(Position);

public abstract record ThrowKey : TypeKey { protected ThrowKey(int slot) : base(slot) { } }
public sealed record ThrowMember : ThrowKey { public ThrowMember() : base(1) => throw new InvalidOperationException("ThrowMember constructor failed."); }
