namespace Framework.Multiplayer
{
    public delegate TContext ContextResolver<in TData, out TContext>(TData submission)
        where TData    : PlayerData
        where TContext : PlayerContext;
}
