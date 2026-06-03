namespace Framework.Commands
{
    // Player/game intent the server validates and executes to author mutations.
    //
    // A command is pure data and server-only (L1): it never crosses the wire — only
    // the mutations its handler emits do. It may carry domain references freely
    // (e.g. an Entity, values), but NOT infrastructure (services / stores / registries)
    // or behavior (delegates / logic): those live on the ICommandHandler (L4). Holding a
    // live ref is safe because Execute is inline + synchronous (L3) — no gap before handling.
    public abstract class Command { }
}
