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
    private const int MaxTypes = 256;
    private const ulong HashBasis = 14695981039346656037UL;
    private const ulong HashPrime = 1099511628211UL;

    private readonly Type[] byId;
    private readonly Dictionary<Type, byte> ids = new Dictionary<Type, byte>();
    private readonly Dictionary<Type, WireType> primitives = new Dictionary<Type, WireType>();

    public WireCatalog(IEnumerable<WireTypes> types)
    {
        foreach (WireType type in WireTypes.BuiltIn.Types.Concat(types.SelectMany(t => t.Types)))
        {
            primitives.TryAdd(type.Message, type);
        }

        List<Type> ordered = new List<Type>
        {
            typeof(CreateEntity), typeof(DeleteEntity), typeof(GroupStart), typeof(GroupEnd), typeof(WireHello),
        };
        ordered.AddRange(primitives.Keys.Except(ordered).OrderBy(KeyOf, StringComparer.Ordinal));
        if (ordered.Count > MaxTypes)
        {
            throw new InvalidOperationException(
                $"The wire catalog has {ordered.Count} types. A 1-byte id gives {MaxTypes} types maximum.");
        }

        byId = ordered.ToArray();
        for (int id = 0; id < byId.Length; id++)
        {
            ids.Add(byId[id], (byte)id);
        }

        Hash = HashOf(byId);
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

    public byte IdOf(Type type)
    {
        if (!ids.TryGetValue(type, out byte id))
        {
            throw new InvalidOperationException(
                $"{type.FullName} is not in the wire catalog. Add its key with WireTypes.Of.");
        }

        return id;
    }

    public void Write(IMessage entry, IBufferWriter<byte> output)
    {
        Type type = entry.GetType();
        byte id = IdOf(type);
        output.GetSpan(1)[0] = id;
        output.Advance(1);
        MemoryPackSerializer.Serialize(type, output, entry);
    }

    public IMessage Read(ReadOnlySpan<byte> frame)
    {
        if (frame.IsEmpty)
        {
            throw new InvalidOperationException("The frame is empty.");
        }

        if (frame[0] >= byId.Length)
        {
            throw new InvalidOperationException($"The frame id {frame[0]} is not in the wire catalog.");
        }

        Type type = byId[frame[0]];
        return MemoryPackSerializer.Deserialize(type, frame.Slice(1)) as IMessage
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

    private static ulong HashOf(IEnumerable<Type> types)
    {
        ulong hash = HashBasis;
        foreach (byte value in Encoding.UTF8.GetBytes(string.Join('\n', types.Select(KeyOf))))
        {
            hash = (hash ^ value) * HashPrime;
        }

        return hash;
    }
}
