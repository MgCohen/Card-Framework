namespace Framework.Routing
{
    public interface IOrdered
    {
        // Lower runs first. Equal values sort by full type name, never by registration order.
        int Order { get; }
    }
}
