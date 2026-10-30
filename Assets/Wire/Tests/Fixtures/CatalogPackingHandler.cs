using Scaffold.AutoPacker;

namespace Framework.Wire.Tests
{
    public sealed class CatalogPackingHandler : IPackingHandler
    {
        readonly Catalog _catalog;

        public CatalogPackingHandler(Catalog catalog) => _catalog = catalog;

        public TTarget Resolve<TSource, TTarget>(TSource source)
        {
            if (source is CardDefinition def && typeof(TTarget) == typeof(int))
                return (TTarget)(object)_catalog.GetId(def);
            if (source is int id && typeof(TTarget) == typeof(CardDefinition))
                return (TTarget)(object)_catalog.GetById(id);
            if (source is TTarget target) return target;
            return default;
        }
    }
}
