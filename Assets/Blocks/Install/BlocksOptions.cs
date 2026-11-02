using Framework.RecordIds;

namespace Framework.Blocks
{
    public sealed class BlocksOptions
    {
        // Server: also registers GroupService. Client: BlockPositions only.
        public Role Role { get; set; } = Role.Server;

        // Also registers the Block types of test assemblies on the Network.
        public bool IncludeTests { get; set; }
    }
}
