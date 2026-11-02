using Framework.RecordIds;
using Scaffold.AutoPacker;

namespace Framework.Blocks
{
    public sealed partial class GroupStart : Block
    {
        [Packed] public BlockId  TriggerId;
        [Packed] public EntityId SourceId;
    }
}
