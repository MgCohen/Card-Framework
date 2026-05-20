namespace Framework.Entities
{
    public static class EntityStoreExtensions
    {
        public static Entity From<TDef>(this EntityStore store, TDef def) where TDef : IEntityDefinition
            => store.Create(def.Type, def.Id);
    }
}
