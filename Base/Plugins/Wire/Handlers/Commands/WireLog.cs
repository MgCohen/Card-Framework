using VSA.Contracts;
using Wire.Contracts.Commands;

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
        if (entry is WireHello)
        {
            throw new InvalidOperationException("A WireHello opens a connection. It is not a log entry.");
        }

        if (entry is WireReply)
        {
            throw new InvalidOperationException("A WireReply goes to one client only. It is not a log entry.");
        }

        catalog.IdOf(entry.GetType());
        entries.Add(entry);
        Appended?.Invoke(entry);
    }
}
