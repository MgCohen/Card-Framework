namespace Framework.Entities
{
    public static class EntityTypes
    {
        public static readonly EntityType Unknown  = EntityTypeRegistry.Register(0, nameof(Unknown));
        public static readonly EntityType Player   = EntityTypeRegistry.Register(2, nameof(Player));
        public static readonly EntityType Zone     = EntityTypeRegistry.Register(3, nameof(Zone));
        public static readonly EntityType Card     = EntityTypeRegistry.Register(4, nameof(Card));
        public static readonly EntityType Modifier = EntityTypeRegistry.Register(5, nameof(Modifier));
    }
}
