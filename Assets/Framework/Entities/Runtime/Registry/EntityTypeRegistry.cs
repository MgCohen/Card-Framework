using Framework.Core;

namespace Framework.Entities
{
    // Registry of entity kinds — a scoped instance (never a static global), so tests and
    // server/client scopes stay isolated. A thin SlotRegistry<EntityType> for DI/naming
    // clarity; all behaviour is inherited (Register<T> / Register(member) / BySlot / Of<T>).
    //
    // WithBaselines() loads the framework kinds. Each composition root calls it explicitly
    // (EntityLayer per scope, TestStack in tests); games register their own kinds on top.
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
