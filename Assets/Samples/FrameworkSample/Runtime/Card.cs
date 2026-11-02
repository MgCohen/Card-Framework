using Framework.RecordIds;

namespace Framework.Samples.FrameworkSample
{
    public sealed record Card(CardId Id, Zone Zone) : IRecord<CardId>;
}
