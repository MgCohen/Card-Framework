using TypeKeys.Domain;
using TypeKeys.Domain.Shared;

namespace TypeKeys.Tests;

// Two custom registries for one root, so the build cannot select one.
public abstract record Shape : TypeKey { protected Shape(int slot) : base(slot) { } }
public sealed record Square() : Shape(1);
public sealed class ShapeRegistryA : TypeKeyRegistry<Shape> { }
public sealed class ShapeRegistryB : TypeKeyRegistry<Shape> { }
