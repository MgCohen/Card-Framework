using System.Buffers;
using System.Text;
using Ecs.Contracts.Commands;
using MemoryPack;
using VSA.Contracts;
using VSA.Contracts.Commands;
using Wire.Contracts.Commands;

namespace Wire.Handlers.Commands;

public sealed class WireCatalog
{
    private const ulong HashBasis = 14695981039346656037UL;
    private const ulong HashPrime = 1099511628211UL;

    private readonly Type[] byId;
    private readonly Dictionary<Type, int> ids = new Dictionary<Type, int>();
    private readonly Dictionary<Type, WireType> primitives = new Dictionary<Type, WireType>();
    private readonly Dictionary<Type, WireMessage> messages = new Dictionary<Type, WireMessage>();

    public WireCatalog(IEnumerable<WireTypes> types)
    {
        WireTypes[] all = types.Prepend(WireTypes.BuiltIn).ToArray();
        foreach (WireType type in all.SelectMany(t => t.Types))
        {
            primitives.TryAdd(type.Message, type);
        }

        foreach (WireMessage message in all.SelectMany(t => t.Messages))
        {
            messages.TryAdd(message.Message, message);
        }

        List<Type> ordered = new List<Type>
        {
            typeof(CreateEntity), typeof(DeleteEntity), typeof(GroupStart), typeof(GroupEnd), typeof(WireHello),
            typeof(WireReply),
        };
        ordered.AddRange(primitives.Keys.Concat(messages.Keys).Except(ordered).OrderBy(KeyOf, StringComparer.Ordinal));
        byId = ordered.ToArray();
        for (int id = 0; id < byId.Length; id++)
        {
            ids.Add(byId[id], id);
        }

        Hash = HashOf(byId.Select(HashKeyOf));
    }

    public int Count => byId.Length;

    public ulong Hash { get; }

    public static string KeyOf(Type type)
    {
        if (!type.IsGenericType)
        {
            return type.FullName ?? type.Name;
        }

        Type definition = type.GetGenericTypeDefinition();
        string arguments = string.Join(",", type.GetGenericArguments().Select(KeyOf));
        return (definition.FullName ?? definition.Name) + "[" + arguments + "]";
    }

    // The result that a WireReply holds: its error, or the MemoryPack value of the reply type.
    public static Result<TReply> ReplyOf<TReply>(WireReply reply)
    {
        if (reply.ErrorCode != null)
        {
            return new Error(reply.ErrorCode, reply.ErrorMessage ?? string.Empty);
        }

        return MemoryPackSerializer.Deserialize<TReply>(reply.Value)!;
    }

    public int IdOf(Type type)
    {
        if (!ids.TryGetValue(type, out int id))
        {
            throw new InvalidOperationException(
                $"{type.FullName} is not in the wire catalog. "
                + "Mark its key with [TypeArgument], or add it with WireTypes.Of.");
        }

        return id;
    }

    public bool IsClientMessage(Type type)
    {
        return messages.ContainsKey(type);
    }

    public void Write(IMessage entry, IBufferWriter<byte> output)
    {
        Type type = entry.GetType();
        WireVarint.Write(IdOf(type), output);
        MemoryPackSerializer.Serialize(type, output, entry);
    }

    public IMessage Read(ReadOnlySpan<byte> frame)
    {
        int id = WireVarint.Read(frame, out int length);
        if (id >= byId.Length)
        {
            throw new InvalidOperationException($"The frame id {id} is not in the wire catalog.");
        }

        Type type = byId[id];
        return MemoryPackSerializer.Deserialize(type, frame.Slice(length)) as IMessage
            ?? throw new InvalidOperationException($"The frame of {type.FullName} has no value.");
    }

    public Task<Error?> ApplyAsync(IMessage entry, ICommandSender bus, WireLog log, CancellationToken ct)
    {
        if (entry is IPrimitive primitive)
        {
            return SendAsync(primitive, bus, ct);
        }

        log.Append(entry);
        return Task.FromResult<Error?>(null);
    }

    public Task<Error?> SendAsync(IPrimitive primitive, ICommandSender bus, CancellationToken ct)
    {
        if (!primitives.TryGetValue(primitive.GetType(), out WireType? type))
        {
            throw new InvalidOperationException($"{primitive.GetType().FullName} is not in the wire catalog.");
        }

        return type.SendAsync(primitive, bus, ct);
    }

    // Runs a client message on the bus and gives the reply for the client that sent it.
    public Task<WireReply> RunAsync(IMessage message, ICommandSender bus, CancellationToken ct)
    {
        if (!messages.TryGetValue(message.GetType(), out WireMessage? type))
        {
            throw new InvalidOperationException($"{message.GetType().FullName} is not a client message of the catalog.");
        }

        return type.RunAsync(message, bus, ct);
    }

    // A client message adds its reply type, because the client reads the reply bytes as that type.
    private string HashKeyOf(Type type)
    {
        return messages.TryGetValue(type, out WireMessage? message)
            ? KeyOf(type) + "->" + KeyOf(message.Reply)
            : KeyOf(type);
    }

    private static ulong HashOf(IEnumerable<string> keys)
    {
        ulong hash = HashBasis;
        foreach (byte value in Encoding.UTF8.GetBytes(string.Join('\n', keys)))
        {
            hash = (hash ^ value) * HashPrime;
        }

        return hash;
    }
}
