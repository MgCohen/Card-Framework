using System.Collections.Generic;
using Framework.Engine;
using Framework.Entities;
using Framework.Requests;
using Framework.States;
using Framework.Tags;
using Framework.Wire;

namespace Framework.Integration.Tests
{
    // Hand-wires the full server engine the way EngineLayer + the feature layers do in
    // production, minus VContainer: one EventStream shared by every service, the standard
    // framework apply-handlers on the receiving end, plus the DrawCard request/response
    // fixtures. Constructing it exercises the GameEngine aggregate ctor (the Engine layer);
    // driving through engine.* exercises the emit -> apply -> read spine across features.
    sealed class EngineStack
    {
        public EventStream Stream { get; }
        public GameEngine  Engine { get; }

        public EngineStack()
        {
            Stream = new EventStream();
            var network = new Network(new NullTransport());

            var tagStore     = new TagStore();
            var entityStore  = new EntityStore();
            var requestStore = new RequestStore();
            var stateStore   = new StateStore();
            var entityTypes  = EntityTypeRegistry.WithBaselines();

            var tags     = new TagService(tagStore, Stream);
            var entities = new EntityService(entityStore, Stream);
            var requests = new RequestService(Stream);
            var states   = new StateService(Stream);

            Engine = new GameEngine(
                Stream, network,
                entityStore, tagStore, requestStore, stateStore,
                entities, tags, requests, states);

            var handlers = new List<IWireEventHandler>
            {
                new EntityCreatedHandler(entityStore, entityTypes),
                new TagSetHandler<int>(tagStore),
                new TagChangeHandler<int>(tagStore),
                new TagSetHandler<bool>(tagStore),
                new TagChangeHandler<bool>(tagStore),
                new TagRemovedHandler(tagStore),
                new DrawCardRequestHandler(requestStore),
                new DrawCardResponseHandler(requestStore, requests),
            };

            // Ctor side-effect: subscribes to Stream.OnAppended. The instance is otherwise unused.
            _ = new HandlerRegistry(Stream, handlers);
        }
    }
}
