using Framework.RecordIds;

namespace Framework.Pipeline
{
    public sealed class PipelineOptions
    {
        public Role Role { get; set; } = Role.Server;
        public int FirstMessageId { get; set; } = 1;
        public bool IncludeTests { get; set; }
    }
}
