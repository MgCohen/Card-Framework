using Ecs.Domain;
using Ecs.Domain.Shared;
using Ecs.Infrastructure;
using Ecs.Tests.Fixtures;

namespace Ecs.Tests;

public sealed class WorldIdTests
{
    [Test]
    public void Create_WithId_UsesThatId()
    {
        World world = new WorldFactory().Create();

        Entity entity = world.Create(new EntityId(7));

        Assert.That(entity.Id, Is.EqualTo(new EntityId(7)));
        Assert.That(world.TryById(new EntityId(7), out _), Is.True);
    }

    [Test]
    public void Create_LiveId_Throws()
    {
        World world = new WorldFactory().Create();
        world.Create(new EntityId(7));

        Assert.Throws<ArgumentException>(new Action(() => world.Create(new EntityId(7))));
    }

    [Test]
    public void Create_ZeroId_Throws()
    {
        World world = new WorldFactory().Create();

        Assert.Throws<ArgumentException>(new Action(() => world.Create(default(EntityId))));
    }

    [Test]
    public void NextId_EmptyWorld_IsOne()
    {
        World world = new WorldFactory().Create();

        Assert.That(world.NextId(), Is.EqualTo(new EntityId(1)));
    }

    [Test]
    public void NextId_AfterCreateWithId_IsOneMore()
    {
        World world = new WorldFactory().Create();
        world.Create(new EntityId(7));

        Assert.That(world.NextId(), Is.EqualTo(new EntityId(8)));
    }

    [Test]
    public void Create_NoId_UsesNextId()
    {
        World world = new WorldFactory().Create();
        world.Create(new EntityId(7));

        Entity entity = world.Create();

        Assert.That(entity.Id, Is.EqualTo(new EntityId(8)));
    }

    [Test]
    public void Create_TwoWorldsSameSequence_SameIds()
    {
        World first = new WorldFactory().Create();
        World second = new WorldFactory().Create();

        EntityId[] firstIds = { first.Create().Id, first.Create(new EntityId(5)).Id, first.Create().Id };
        EntityId[] secondIds = { second.Create().Id, second.Create(new EntityId(5)).Id, second.Create().Id };

        Assert.That(secondIds, Is.EqualTo(firstIds));
    }

    [Test]
    public void Is_EqualValue_True()
    {
        World world = new WorldFactory().Create();
        Entity entity = world.Create();
        world.Set(entity, new Position { X = 1, Y = 2 });

        Assert.That(world.Is(entity, new Position { X = 1, Y = 2 }), Is.True);
        Assert.That(world.Reader().Is(entity, new Position { X = 1, Y = 2 }), Is.True);
    }

    [Test]
    public void Is_OtherValue_False()
    {
        World world = new WorldFactory().Create();
        Entity entity = world.Create();
        world.Set(entity, new Position { X = 1, Y = 2 });

        Assert.That(world.Is(entity, new Position { X = 1, Y = 3 }), Is.False);
    }

    [Test]
    public void Is_Absent_False()
    {
        World world = new WorldFactory().Create();
        Entity entity = world.Create();

        Assert.That(world.Is(entity, default(Position)), Is.False);
    }
}
