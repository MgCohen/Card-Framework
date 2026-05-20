namespace Framework.Multiplayer
{
    // Base submission shape. Clients build one and pass to session.SetSelfData.
    // Games extend with extra fields by subclassing (e.g. LoadoutId, AvatarId) and using
    // the generic Session<TData, TContext>. Note for now: subclass extension fields don't
    // yet survive the wire — Wire has no polymorphic discriminator on WireEvent. Subclass
    // properties round-trip locally on the submitter; remote peers only see the base
    // fields encoded in PlayerDataEvent. Tracked as bl-polymorphic-wire-events.
    public class PlayerData
    {
        public ulong NetworkId;
        public ulong PlayerId;
    }
}
