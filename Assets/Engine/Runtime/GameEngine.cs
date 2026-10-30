using Framework.Commands;
using Framework.Entities;
using Framework.Requests;
using Framework.States;
using Framework.Tags;
using Framework.Wire;

namespace Framework.Engine
{
    public sealed class GameEngine
    {
        public EventStream          Stream       { get; }
        public Network              Network      { get; }
        public IReadOnlyEntityStore EntityStore  { get; }
        public IReadOnlyTagStore    TagStore     { get; }
        public IReadOnlyRequestStore RequestStore { get; }
        public IReadOnlyStateStore  StateStore   { get; }
        public EntityService        Entities     { get; }
        public TagService           Tags         { get; }
        public RequestService       Requests     { get; }
        public StateService         States       { get; }
        public CommandService       Commands     { get; }

        public GameEngine(
            EventStream stream, Network network,
            EntityStore entityStore, TagStore tagStore, RequestStore requestStore, StateStore stateStore,
            EntityService entities, TagService tags, RequestService requests, StateService states,
            CommandService commands)
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
            Commands     = commands;
        }
    }
}
