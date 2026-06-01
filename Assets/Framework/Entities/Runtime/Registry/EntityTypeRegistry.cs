using Framework.Core;

namespace Framework.Entities
{
    public sealed class EntityTypeRegistry : SlotRegistry<EntityType>
    {
        public static EntityTypeRegistry WithBaselines()
        {
            var registry = new EntityTypeRegistry();
            registry.Register<Unknown>();
            registry.Register<Player>();
            registry.Register<Zone>();
            registry.Register<Card>();
            registry.Register<Modifier>();
            return registry;
        }
    }
}
