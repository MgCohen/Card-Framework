using System.Globalization;
using System.Reflection;

namespace Trace.Handlers.Commands;

public static class TraceText
{
    private const int TagWidth = 10;

    public static IReadOnlyList<string> Legend { get; } = new[]
    {
        "LEGEND",
        "  [SEND]    the message passed its validators, and its handler starts: msg id, parent msg id, payload",
        "  [REJECT]  a validator or a Before event stopped the message; its handler did not run",
        "  [RESULT]  the value that the handler returned: Ok, Ok(value) or Error(code).",
        "            It is not the end of a step: the handler of the parent continues after it",
        "  [FAULT]   the handler or a hook threw an exception",
        "  [WIRE]    the WireLog recorded an accepted primitive: entry number, msg id of the primitive",
        "  [WORLD]   World values",
        "  The indent shows the parent. msg = Envelope.Id, parent = Envelope.Parent.Id",
    };

    public static IEnumerable<string> Lines(IEnumerable<TraceEvent> events)
    {
        TraceEvent[] all = events.ToArray();
        int width = all.Length == 0 ? 0 : all.Max(e => Head(e).Length) + 2;
        int ids = all.Length == 0 ? 0 : all.Max(e => e.Id.ToString(CultureInfo.InvariantCulture).Length);
        return all.Select(e => Line(e, width, ids));
    }

    public static string Name(Type type)
    {
        if (!type.IsGenericType)
        {
            return type.Name;
        }

        string name = type.Name.Substring(0, type.Name.IndexOf('`', StringComparison.Ordinal));
        return $"{name}<{string.Join(", ", type.GetGenericArguments().Select(Name))}>";
    }

    public static string Payload(object message)
    {
        IEnumerable<string> fields = Properties(message.GetType())
            .Select(p => $"{p.Name}: {Value(p.GetValue(message))}");
        return $"{{ {string.Join(", ", fields)} }}";
    }

    public static string Value(object? value)
    {
        if (value is null)
        {
            return "null";
        }

        Type type = value.GetType();
        if (type.IsPrimitive || type.IsEnum || value is string or decimal)
        {
            return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
        }

        PropertyInfo[] properties = Properties(type);
        return properties.Length == 1 && properties[0].Name == "Value"
            ? Value(properties[0].GetValue(value))
            : Payload(value);
    }

    private static PropertyInfo[] Properties(Type type)
    {
        return type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.GetIndexParameters().Length == 0 && p.Name != "EqualityContract")
            .ToArray();
    }

    private static string Head(TraceEvent e)
    {
        string tag = $"[{e.Kind.ToString().ToUpperInvariant()}]";
        return new string(' ', e.Depth * 2) + tag.PadRight(TagWidth) + Name(e.Message.GetType());
    }

    private static string Line(TraceEvent e, int width, int ids)
    {
        string id = e.Id == 0 ? "-" : e.Id.ToString(CultureInfo.InvariantCulture);
        string msg = $"msg={id}".PadRight(ids + 6);
        string head = Head(e).PadRight(width) + msg;
        switch (e.Kind)
        {
            case TraceKind.Send:
                return $"{head}{Parent(e, ids)}payload={Payload(e.Message)}";
            case TraceKind.Reject:
                return $"{head}{Parent(e, ids)}payload={Payload(e.Message)}  {e.Outcome}";
            default:
                return $"{head}{e.Outcome}";
        }
    }

    private static string Parent(TraceEvent e, int ids)
    {
        string parent = e.Parent is null
            ? string.Empty
            : $"parent={e.Parent.Value.ToString(CultureInfo.InvariantCulture)}";
        return parent.PadRight(ids + 9);
    }
}
