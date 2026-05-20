namespace Framework.Multiplayer
{
    // Host-side hook that turns a client's PlayerData submission into the authoritative
    // PlayerContext that gets broadcast. Apps inject one when constructing a Session;
    // the default at Session<PlayerData, PlayerContext> copies base fields straight across.
    public delegate TContext ContextResolver<in TData, out TContext>(TData submission)
        where TData    : PlayerData
        where TContext : PlayerContext;
}
