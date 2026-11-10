using Ecs.Domain.Shared;
using Wire.Handlers.Commands;

namespace Wire.Tests.Fixtures;

// All: 64 components and 64 tags give 256 primitive types; with the 5 built-in types, the catalog has 261.
// Max255: 62 components and 63 tags give 250; with the built-in types, the catalog has 255. The count of built-in types is odd,
// so the catalog cannot have exactly 256 types.
public struct C00 : IComponent
{
    public int Value { get; init; }
}

public struct C01 : IComponent
{
    public int Value { get; init; }
}

public struct C02 : IComponent
{
    public int Value { get; init; }
}

public struct C03 : IComponent
{
    public int Value { get; init; }
}

public struct C04 : IComponent
{
    public int Value { get; init; }
}

public struct C05 : IComponent
{
    public int Value { get; init; }
}

public struct C06 : IComponent
{
    public int Value { get; init; }
}

public struct C07 : IComponent
{
    public int Value { get; init; }
}

public struct C08 : IComponent
{
    public int Value { get; init; }
}

public struct C09 : IComponent
{
    public int Value { get; init; }
}

public struct C10 : IComponent
{
    public int Value { get; init; }
}

public struct C11 : IComponent
{
    public int Value { get; init; }
}

public struct C12 : IComponent
{
    public int Value { get; init; }
}

public struct C13 : IComponent
{
    public int Value { get; init; }
}

public struct C14 : IComponent
{
    public int Value { get; init; }
}

public struct C15 : IComponent
{
    public int Value { get; init; }
}

public struct C16 : IComponent
{
    public int Value { get; init; }
}

public struct C17 : IComponent
{
    public int Value { get; init; }
}

public struct C18 : IComponent
{
    public int Value { get; init; }
}

public struct C19 : IComponent
{
    public int Value { get; init; }
}

public struct C20 : IComponent
{
    public int Value { get; init; }
}

public struct C21 : IComponent
{
    public int Value { get; init; }
}

public struct C22 : IComponent
{
    public int Value { get; init; }
}

public struct C23 : IComponent
{
    public int Value { get; init; }
}

public struct C24 : IComponent
{
    public int Value { get; init; }
}

public struct C25 : IComponent
{
    public int Value { get; init; }
}

public struct C26 : IComponent
{
    public int Value { get; init; }
}

public struct C27 : IComponent
{
    public int Value { get; init; }
}

public struct C28 : IComponent
{
    public int Value { get; init; }
}

public struct C29 : IComponent
{
    public int Value { get; init; }
}

public struct C30 : IComponent
{
    public int Value { get; init; }
}

public struct C31 : IComponent
{
    public int Value { get; init; }
}

public struct C32 : IComponent
{
    public int Value { get; init; }
}

public struct C33 : IComponent
{
    public int Value { get; init; }
}

public struct C34 : IComponent
{
    public int Value { get; init; }
}

public struct C35 : IComponent
{
    public int Value { get; init; }
}

public struct C36 : IComponent
{
    public int Value { get; init; }
}

public struct C37 : IComponent
{
    public int Value { get; init; }
}

public struct C38 : IComponent
{
    public int Value { get; init; }
}

public struct C39 : IComponent
{
    public int Value { get; init; }
}

public struct C40 : IComponent
{
    public int Value { get; init; }
}

public struct C41 : IComponent
{
    public int Value { get; init; }
}

public struct C42 : IComponent
{
    public int Value { get; init; }
}

public struct C43 : IComponent
{
    public int Value { get; init; }
}

public struct C44 : IComponent
{
    public int Value { get; init; }
}

public struct C45 : IComponent
{
    public int Value { get; init; }
}

public struct C46 : IComponent
{
    public int Value { get; init; }
}

public struct C47 : IComponent
{
    public int Value { get; init; }
}

public struct C48 : IComponent
{
    public int Value { get; init; }
}

public struct C49 : IComponent
{
    public int Value { get; init; }
}

public struct C50 : IComponent
{
    public int Value { get; init; }
}

public struct C51 : IComponent
{
    public int Value { get; init; }
}

public struct C52 : IComponent
{
    public int Value { get; init; }
}

public struct C53 : IComponent
{
    public int Value { get; init; }
}

public struct C54 : IComponent
{
    public int Value { get; init; }
}

public struct C55 : IComponent
{
    public int Value { get; init; }
}

public struct C56 : IComponent
{
    public int Value { get; init; }
}

public struct C57 : IComponent
{
    public int Value { get; init; }
}

public struct C58 : IComponent
{
    public int Value { get; init; }
}

public struct C59 : IComponent
{
    public int Value { get; init; }
}

public struct C60 : IComponent
{
    public int Value { get; init; }
}

public struct C61 : IComponent
{
    public int Value { get; init; }
}

public struct C62 : IComponent
{
    public int Value { get; init; }
}

public struct C63 : IComponent
{
    public int Value { get; init; }
}

public struct T00 : ITag
{
}

public struct T01 : ITag
{
}

public struct T02 : ITag
{
}

public struct T03 : ITag
{
}

public struct T04 : ITag
{
}

public struct T05 : ITag
{
}

public struct T06 : ITag
{
}

public struct T07 : ITag
{
}

public struct T08 : ITag
{
}

public struct T09 : ITag
{
}

public struct T10 : ITag
{
}

public struct T11 : ITag
{
}

public struct T12 : ITag
{
}

public struct T13 : ITag
{
}

public struct T14 : ITag
{
}

public struct T15 : ITag
{
}

public struct T16 : ITag
{
}

public struct T17 : ITag
{
}

public struct T18 : ITag
{
}

public struct T19 : ITag
{
}

public struct T20 : ITag
{
}

public struct T21 : ITag
{
}

public struct T22 : ITag
{
}

public struct T23 : ITag
{
}

public struct T24 : ITag
{
}

public struct T25 : ITag
{
}

public struct T26 : ITag
{
}

public struct T27 : ITag
{
}

public struct T28 : ITag
{
}

public struct T29 : ITag
{
}

public struct T30 : ITag
{
}

public struct T31 : ITag
{
}

public struct T32 : ITag
{
}

public struct T33 : ITag
{
}

public struct T34 : ITag
{
}

public struct T35 : ITag
{
}

public struct T36 : ITag
{
}

public struct T37 : ITag
{
}

public struct T38 : ITag
{
}

public struct T39 : ITag
{
}

public struct T40 : ITag
{
}

public struct T41 : ITag
{
}

public struct T42 : ITag
{
}

public struct T43 : ITag
{
}

public struct T44 : ITag
{
}

public struct T45 : ITag
{
}

public struct T46 : ITag
{
}

public struct T47 : ITag
{
}

public struct T48 : ITag
{
}

public struct T49 : ITag
{
}

public struct T50 : ITag
{
}

public struct T51 : ITag
{
}

public struct T52 : ITag
{
}

public struct T53 : ITag
{
}

public struct T54 : ITag
{
}

public struct T55 : ITag
{
}

public struct T56 : ITag
{
}

public struct T57 : ITag
{
}

public struct T58 : ITag
{
}

public struct T59 : ITag
{
}

public struct T60 : ITag
{
}

public struct T61 : ITag
{
}

public struct T62 : ITag
{
}

public struct T63 : ITag
{
}

public static class ManyKeys
{
    public static WireTypes Max255()
    {
        return WireTypes.Of(t => t
            .Component<C00>()
            .Component<C01>()
            .Component<C02>()
            .Component<C03>()
            .Component<C04>()
            .Component<C05>()
            .Component<C06>()
            .Component<C07>()
            .Component<C08>()
            .Component<C09>()
            .Component<C10>()
            .Component<C11>()
            .Component<C12>()
            .Component<C13>()
            .Component<C14>()
            .Component<C15>()
            .Component<C16>()
            .Component<C17>()
            .Component<C18>()
            .Component<C19>()
            .Component<C20>()
            .Component<C21>()
            .Component<C22>()
            .Component<C23>()
            .Component<C24>()
            .Component<C25>()
            .Component<C26>()
            .Component<C27>()
            .Component<C28>()
            .Component<C29>()
            .Component<C30>()
            .Component<C31>()
            .Component<C32>()
            .Component<C33>()
            .Component<C34>()
            .Component<C35>()
            .Component<C36>()
            .Component<C37>()
            .Component<C38>()
            .Component<C39>()
            .Component<C40>()
            .Component<C41>()
            .Component<C42>()
            .Component<C43>()
            .Component<C44>()
            .Component<C45>()
            .Component<C46>()
            .Component<C47>()
            .Component<C48>()
            .Component<C49>()
            .Component<C50>()
            .Component<C51>()
            .Component<C52>()
            .Component<C53>()
            .Component<C54>()
            .Component<C55>()
            .Component<C56>()
            .Component<C57>()
            .Component<C58>()
            .Component<C59>()
            .Component<C60>()
            .Component<C61>()
            .Tag<T00>()
            .Tag<T01>()
            .Tag<T02>()
            .Tag<T03>()
            .Tag<T04>()
            .Tag<T05>()
            .Tag<T06>()
            .Tag<T07>()
            .Tag<T08>()
            .Tag<T09>()
            .Tag<T10>()
            .Tag<T11>()
            .Tag<T12>()
            .Tag<T13>()
            .Tag<T14>()
            .Tag<T15>()
            .Tag<T16>()
            .Tag<T17>()
            .Tag<T18>()
            .Tag<T19>()
            .Tag<T20>()
            .Tag<T21>()
            .Tag<T22>()
            .Tag<T23>()
            .Tag<T24>()
            .Tag<T25>()
            .Tag<T26>()
            .Tag<T27>()
            .Tag<T28>()
            .Tag<T29>()
            .Tag<T30>()
            .Tag<T31>()
            .Tag<T32>()
            .Tag<T33>()
            .Tag<T34>()
            .Tag<T35>()
            .Tag<T36>()
            .Tag<T37>()
            .Tag<T38>()
            .Tag<T39>()
            .Tag<T40>()
            .Tag<T41>()
            .Tag<T42>()
            .Tag<T43>()
            .Tag<T44>()
            .Tag<T45>()
            .Tag<T46>()
            .Tag<T47>()
            .Tag<T48>()
            .Tag<T49>()
            .Tag<T50>()
            .Tag<T51>()
            .Tag<T52>()
            .Tag<T53>()
            .Tag<T54>()
            .Tag<T55>()
            .Tag<T56>()
            .Tag<T57>()
            .Tag<T58>()
            .Tag<T59>()
            .Tag<T60>()
            .Tag<T61>()
            .Tag<T62>());
    }

    public static WireTypes All()
    {
        return WireTypes.Of(t => t
            .Component<C00>()
            .Component<C01>()
            .Component<C02>()
            .Component<C03>()
            .Component<C04>()
            .Component<C05>()
            .Component<C06>()
            .Component<C07>()
            .Component<C08>()
            .Component<C09>()
            .Component<C10>()
            .Component<C11>()
            .Component<C12>()
            .Component<C13>()
            .Component<C14>()
            .Component<C15>()
            .Component<C16>()
            .Component<C17>()
            .Component<C18>()
            .Component<C19>()
            .Component<C20>()
            .Component<C21>()
            .Component<C22>()
            .Component<C23>()
            .Component<C24>()
            .Component<C25>()
            .Component<C26>()
            .Component<C27>()
            .Component<C28>()
            .Component<C29>()
            .Component<C30>()
            .Component<C31>()
            .Component<C32>()
            .Component<C33>()
            .Component<C34>()
            .Component<C35>()
            .Component<C36>()
            .Component<C37>()
            .Component<C38>()
            .Component<C39>()
            .Component<C40>()
            .Component<C41>()
            .Component<C42>()
            .Component<C43>()
            .Component<C44>()
            .Component<C45>()
            .Component<C46>()
            .Component<C47>()
            .Component<C48>()
            .Component<C49>()
            .Component<C50>()
            .Component<C51>()
            .Component<C52>()
            .Component<C53>()
            .Component<C54>()
            .Component<C55>()
            .Component<C56>()
            .Component<C57>()
            .Component<C58>()
            .Component<C59>()
            .Component<C60>()
            .Component<C61>()
            .Component<C62>()
            .Component<C63>()
            .Tag<T00>()
            .Tag<T01>()
            .Tag<T02>()
            .Tag<T03>()
            .Tag<T04>()
            .Tag<T05>()
            .Tag<T06>()
            .Tag<T07>()
            .Tag<T08>()
            .Tag<T09>()
            .Tag<T10>()
            .Tag<T11>()
            .Tag<T12>()
            .Tag<T13>()
            .Tag<T14>()
            .Tag<T15>()
            .Tag<T16>()
            .Tag<T17>()
            .Tag<T18>()
            .Tag<T19>()
            .Tag<T20>()
            .Tag<T21>()
            .Tag<T22>()
            .Tag<T23>()
            .Tag<T24>()
            .Tag<T25>()
            .Tag<T26>()
            .Tag<T27>()
            .Tag<T28>()
            .Tag<T29>()
            .Tag<T30>()
            .Tag<T31>()
            .Tag<T32>()
            .Tag<T33>()
            .Tag<T34>()
            .Tag<T35>()
            .Tag<T36>()
            .Tag<T37>()
            .Tag<T38>()
            .Tag<T39>()
            .Tag<T40>()
            .Tag<T41>()
            .Tag<T42>()
            .Tag<T43>()
            .Tag<T44>()
            .Tag<T45>()
            .Tag<T46>()
            .Tag<T47>()
            .Tag<T48>()
            .Tag<T49>()
            .Tag<T50>()
            .Tag<T51>()
            .Tag<T52>()
            .Tag<T53>()
            .Tag<T54>()
            .Tag<T55>()
            .Tag<T56>()
            .Tag<T57>()
            .Tag<T58>()
            .Tag<T59>()
            .Tag<T60>()
            .Tag<T61>()
            .Tag<T62>()
            .Tag<T63>());
    }
}
