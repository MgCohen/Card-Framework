using Framework.Entities;
using Framework.Tags;
using Framework.Wire;

namespace Framework.Engine
{
    // Composition-shaped façade — typed properties per framework service. Discoverable
    // surface for end-user code; internal plumbing still injects services directly.
    public sealed class GameEngine
    {
        public EventStream  Stream   { get; }
        public Network      Network  { get; }
        public EntityService Entities { get; }
        public TagService    Tags     { get; }

        public GameEngine(EventStream stream, Network network, EntityService entities, TagService tags)
        {
            Stream   = stream;
            Network  = network;
            Entities = entities;
            Tags     = tags;
        }
    }
}
