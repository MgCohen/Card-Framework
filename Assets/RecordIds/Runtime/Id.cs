using Scaffold.AutoPacker;

namespace Framework.RecordIds
{
    [AutoPack]
    public abstract partial record Id
    {
        // -1 is "no id" and packs as null; 0 is "never set" (a bug) and stays visible.
        [Packed] public int Value { get; init; } = -1;
    }
}
