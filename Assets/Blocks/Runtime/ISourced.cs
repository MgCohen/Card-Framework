using Framework.RecordIds;

namespace Framework.Blocks
{
    // A message that gives its source entity. The caller passes Source to GroupService.Open.
    public interface ISourced
    {
        IEntity Source { get; }
    }
}
