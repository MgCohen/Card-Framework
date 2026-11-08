namespace Pipeline.Handlers.Commands;

internal interface IRouted
{
    Type RouteType { get; }
}
