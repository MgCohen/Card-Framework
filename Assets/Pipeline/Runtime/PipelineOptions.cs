using Framework.RecordIds;

namespace Framework.Pipeline
{
    public sealed class PipelineOptions
    {
        // Only Role.Server is valid in v0: AddPipeline throws for Role.Client.
        public Role Role { get; set; } = Role.Server;
        public int FirstMessageId { get; set; } = 1;
        public bool IncludeTests { get; set; }
    }
}
