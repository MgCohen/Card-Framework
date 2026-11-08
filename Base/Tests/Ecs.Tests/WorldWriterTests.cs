using Ecs.Domain;
using Ecs.Tests.Fixtures;

namespace Ecs.Tests;

public sealed class WorldWriterTests
{
    [Test]
    public void Create_AddsALiveEntity()
    {
        World world = new WorldFactory().Create();

        world.Create();
        world.Create();

        Assert.That(world.Count, Is.EqualTo(2));
    }

    [Test]
    public void Delete_RemovesTheEntity()
    {
        World world = new WorldFactory().Create();
        Entity entity = world.Create();

        Assert.That(world.Delete(entity), Is.True);
        Assert.That(world.Count, Is.Zero);
    }

    [Test]
    public void Delete_DeletedEntity_Throws()
    {
        World world = new WorldFactory().Create();
        Entity entity = world.Create();
        world.Delete(entity);

        Assert.Throws<InvalidOperationException>(new Action(() => world.Delete(entity)));
    }

    [Test]
    public void Create_AfterDelete_NeverReusesAnId()
    {
        World world = new WorldFactory().Create();
        Entity first = world.Create();
        world.Delete(first);

        Entity second = world.Create();

        Assert.That(second.Id, Is.Not.EqualTo(first.Id));
    }

    [Test]
    public void Set_NewComponent_WritesAndReturnsTrue()
    {
        World world = new WorldFactory().Create();
        Entity entity = world.Create();

        Assert.That(world.Set(entity, new Position { X = 1, Y = 2 }), Is.True);
        Assert.That(world.Get<Position>(entity), Is.EqualTo(new Position { X = 1, Y = 2 }));
    }

    [Test]
    public void Set_EqualValue_SkipsAndReturnsFalse()
    {
        World world = new WorldFactory().Create();
        Entity entity = world.Create();
        world.Set(entity, new Position { X = 1, Y = 2 });

        Assert.That(world.Set(entity, new Position { X = 1, Y = 2 }), Is.False);
    }

    [Test]
    public void Set_ChangedValue_WritesAndReturnsTrue()
    {
        World world = new WorldFactory().Create();
        Entity entity = world.Create();
        world.Set(entity, new Position { X = 1, Y = 2 });

        Assert.That(world.Set(entity, new Position { X = 1, Y = 3 }), Is.True);
        Assert.That(world.Get<Position>(entity).Y, Is.EqualTo(3));
    }

    [Test]
    public void SetInt_WritesTheValueOfTheKey()
    {
        World world = new WorldFactory().Create();
        Entity entity = world.Create();

        Assert.That(world.Set<Health>(entity, 27), Is.True);
        Assert.That(world.Get<Health>(entity).Value, Is.EqualTo(27));
        Assert.That(world.Set<Health>(entity, 27), Is.False);
        Assert.That(world.Set<Health>(entity, 26), Is.True);
        Assert.That(world.Get<Health>(entity).Value, Is.EqualTo(26));
    }

    [Test]
    public void Remove_ReturnsTrueOnlyIfPresent()
    {
        World world = new WorldFactory().Create();
        Entity entity = world.Create();
        world.Set<Health>(entity, 1);

        Assert.That(world.Remove<Health>(entity), Is.True);
        Assert.That(world.Has<Health>(entity), Is.False);
        Assert.That(world.Remove<Health>(entity), Is.False);
    }

    [Test]
    public void AddTag_ReturnsTrueOnlyIfAbsent()
    {
        World world = new WorldFactory().Create();
        Entity entity = world.Create();

        Assert.That(world.AddTag<CardTag>(entity), Is.True);
        Assert.That(world.HasTag<CardTag>(entity), Is.True);
        Assert.That(world.AddTag<CardTag>(entity), Is.False);
    }

    [Test]
    public void RemoveTag_ReturnsTrueOnlyIfPresent()
    {
        World world = new WorldFactory().Create();
        Entity entity = world.Create();
        world.AddTag<CardTag>(entity);

        Assert.That(world.RemoveTag<CardTag>(entity), Is.True);
        Assert.That(world.HasTag<CardTag>(entity), Is.False);
        Assert.That(world.RemoveTag<CardTag>(entity), Is.False);
    }
}
