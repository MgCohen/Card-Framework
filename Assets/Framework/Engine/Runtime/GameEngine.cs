using Framework.Entities;
using Framework.Requests;
using Framework.States;
using Framework.Tags;
using Framework.Wire;

namespace Framework.Engine
{
    // Server-only composition handle. Exposes the substrate (Stream, Network, stores) plus
    // the command services that author state. End-user game-logic code uses this surface;
    // internal plumbing still ctor-injects pieces directly.
    //
    // No client equivalent: the client is dumb today (events → handlers → stores) and the
    // pieces it needs (EventStream, EntityStore, TagStore, RequestStore) inject cleanly on
    // their own. When the client acquires multiple coordinated concerns worth composing,
    // that's when a client-shaped handle will earn its name — not before.
    public sealed class GameEngine
    {
        public EventStream    Stream       { get; }
        public Network        Network      { get; }
        public EntityStore    EntityStore  { get; }
        public TagStore       TagStore     { get; }
        public RequestStore   RequestStore { get; }
        public StateStore     StateStore   { get; }
        public EntityService  Entities     { get; }
        public TagService     Tags         { get; }
        public RequestService Requests     { get; }
        public StateService   States       { get; }

        public GameEngine(
            EventStream stream, Network network,
            EntityStore entityStore, TagStore tagStore, RequestStore requestStore, StateStore stateStore,
            EntityService entities, TagService tags, RequestService requests, StateService states)
        {
            Stream       = stream;
            Network      = network;
            EntityStore  = entityStore;
            TagStore     = tagStore;
            RequestStore = requestStore;
            StateStore   = stateStore;
            Entities     = entities;
            Tags         = tags;
            Requests     = requests;
            States       = states;
        }
    }
}
