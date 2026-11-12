using Ecs.Domain.Shared;

namespace Ecs.Tests;

public sealed class EntityIdTests
{
    [Test]
    public void EntityId_SameValue_IsEqual()
    {
        Assert.That(new EntityId(3), Is.EqualTo(new EntityId(3)));
        Assert.That(new EntityId(3), Is.Not.EqualTo(new EntityId(4)));
    }

    [Test]
    public void EntityId_Default_IsTheNullId()
    {
        Assert.That(default(EntityId).Value, Is.Zero);
    }
}
