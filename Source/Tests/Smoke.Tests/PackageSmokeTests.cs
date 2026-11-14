using Friflo.Engine.ECS;
using MemoryPack;

namespace Smoke.Tests;

public sealed partial class PackageSmokeTests
{
    [Test]
    public void Friflo_CreatesEntityWithComponent()
    {
        var store = new EntityStore();

        var entity = store.CreateEntity(new Health { Value = 27 });

        Assert.That(entity.GetComponent<Health>().Value, Is.EqualTo(27));
    }

    [Test]
    public void MemoryPack_RoundTripsRecord()
    {
        var card = new CardRecord(7, "Fireball");

        var bytes = MemoryPackSerializer.Serialize(card);
        var copy = MemoryPackSerializer.Deserialize<CardRecord>(bytes);

        Assert.That(copy, Is.EqualTo(card));
    }

    private struct Health : IComponent
    {
        public int Value;
    }

    [MemoryPackable]
    private partial record CardRecord(int Id, string Name);
}
