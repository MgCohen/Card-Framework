using System;
using NUnit.Framework;

namespace Framework.Entities.Tests
{
    // EntityType-specific registry behaviour. Generic SlotRegistry mechanics (BySlot/TryBySlot/
    // dup-slot) are covered in Framework.Core.Tests; here we cover baseline loading, the Of<T>
    // code path, and game-side extension on top of the baselines.
    public class EntityTypeRegistryTests
    {
        sealed record CustomKind() : EntityType(8000);

        [Test]
        public void WithBaselines_ResolvesByType()
        {
            var registry = EntityTypeRegistry.WithBaselines();
            Assert.AreEqual(new Card(), registry.Of<Card>());
        }

        [Test]
        public void Register_GameKind_ResolvesBySlotAndType()
        {
            var registry = EntityTypeRegistry.WithBaselines();
            registry.Register<CustomKind>();

            Assert.AreEqual(new CustomKind(), registry.BySlot(8000));
            Assert.AreEqual(new CustomKind(), registry.Of<CustomKind>());
        }

        [Test]
        public void Register_OverBaselineSlot_Throws()
        {
            var registry = EntityTypeRegistry.WithBaselines();
            Assert.Throws<InvalidOperationException>(() => registry.Register<Card>());
        }
    }
}
