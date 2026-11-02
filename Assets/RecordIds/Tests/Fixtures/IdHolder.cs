using Scaffold.AutoPacker;

namespace Framework.RecordIds.Tests
{
    [AutoPack]
    public partial class IdHolder
    {
        [Packed] public AId A;
        [Packed] public BId B;
    }
}
