using Ecs.Domain;
using Ecs.Domain.Shared;

namespace Ecs.Tests;

public sealed class EntityTests
{
    [Test]
    public void Entity_Default_IsNullWithTheNullId()
    {
        Entity entity = default;

        Assert.That(entity.IsNull, Is.True);
        Assert.That(entity.Id, Is.EqualTo(default(EntityId)));
    }

    [Test]
    public void Entity_Created_IsNotNullAndHasAnId()
    {
        World world = new World();

        Entity entity = world.Create();

        Assert.That(entity.IsNull, Is.False);
        Assert.That(entity.Id.Value, Is.GreaterThan(0));
    }

    [Test]
    public void Entity_Deleted_IsNull()
    {
        World world = new World();
        Entity entity = world.Create();

        world.Delete(entity);

        Assert.That(entity.IsNull, Is.True);
    }

    [Test]
    public void Entity_SameEntity_IsEqual()
    {
        World world = new World();
        Entity entity = world.Create();

        Entity found = world.ById(entity.Id);

        Assert.That(found == entity, Is.True);
        Assert.That(found.Equals((object)entity), Is.True);
        Assert.That(found.GetHashCode(), Is.EqualTo(entity.GetHashCode()));
    }

    [Test]
    public void Entity_OtherEntity_IsNotEqual()
    {
        World world = new World();

        Entity first = world.Create();
        Entity second = world.Create();

        Assert.That(first != second, Is.True);
    }

    [Test]
    public void Entity_SameIdInTwoWorlds_IsNotEqual()
    {
        Entity first = new World().Create();
        Entity second = new World().Create();

        Assert.That(first.Id, Is.EqualTo(second.Id));
        Assert.That(first == second, Is.False);
    }
}
