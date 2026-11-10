using VSA.Contracts;

namespace Wire.Handlers.Commands;

public sealed class WireLog
{
    private readonly List<IMessage> entries = new List<IMessage>();
    private readonly WireCatalog catalog;

    public WireLog(WireCatalog catalog)
    {
        this.catalog = catalog;
    }

    public IReadOnlyList<IMessage> Entries => entries;

    public event Action<IMessage>? Appended;

    public void Append(IMessage entry)
    {
        catalog.IdOf(entry.GetType());
        entries.Add(entry);
        Appended?.Invoke(entry);
    }
}
