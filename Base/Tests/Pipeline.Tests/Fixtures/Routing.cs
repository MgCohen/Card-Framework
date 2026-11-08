using Pipeline.Handlers.Commands;

namespace Pipeline.Tests;

internal interface INoteReader : IRouted
{
}

internal interface ITagged
{
}

internal abstract class Note
{
}

internal sealed class Hello : Note
{
}

internal sealed class Bye : Note, ITagged
{
}

internal sealed class Gen<T> : Note
{
}

internal abstract class AbstractReader : INoteReader
{
    public Type RouteType => typeof(Hello);
}

internal sealed class ByeReader : INoteReader
{
    public Type RouteType => typeof(Bye);
}

internal sealed class GenReader : INoteReader
{
    public Type RouteType => typeof(Gen<int>);
}

internal sealed class HelloReader : INoteReader
{
    public Type RouteType => typeof(Hello);
}

internal sealed class OpenReader<T> : INoteReader
{
    public Type RouteType => typeof(Gen<T>);
}

internal class Probe : IRouted, IOrdered
{
    public Probe(Type routeType, int order = 0)
    {
        RouteType = routeType;
        Order = order;
    }

    public Type RouteType { get; }

    public int Order { get; }
}

internal sealed class OtherProbe : Probe
{
    public OtherProbe(Type routeType, int order = 0)
        : base(routeType, order)
    {
    }
}
