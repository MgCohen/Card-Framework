using Ecs.Domain;
using Ecs.Domain.Shared;
using Ecs.Infrastructure;
using Ecs.Tests.Fixtures;

namespace Ecs.Tests;

public sealed class WorldReaderTests
{
    [Test]
    public void Reader_SeesTheWritesOfItsWorld()
    {
        World world = new WorldFactory().Create();
        Entity entity = world.Create();
        world.Set(entity, new Health { Value = 27 });
        world.AddTag<CardTag>(entity);

        IWorldReader reader = world.Reader();

        Assert.That(reader.Count, Is.EqualTo(1));
        Assert.That(reader.Get<Health>(entity).Value, Is.EqualTo(27));
        Assert.That(reader.Has<Health>(entity), Is.True);
        Assert.That(reader.HasTag<CardTag>(entity), Is.True);
    }

    [Test]
    public void ById_LiveEntity_ReturnsIt()
    {
        World world = new WorldFactory().Create();
        Entity entity = world.Create();

        Assert.That(world.Reader().ById(entity.Id), Is.EqualTo(entity));
        Assert.That(world.Reader().TryById(entity.Id, out Entity found), Is.True);
        Assert.That(found, Is.EqualTo(entity));
    }

    [Test]
    public void ById_UnknownId_ThrowsKeyNotFound()
    {
        World world = new WorldFactory().Create();

        Assert.Throws<KeyNotFoundException>(new Action(() => world.Reader().ById(new EntityId(999))));
        Assert.Throws<KeyNotFoundException>(new Action(() => world.Reader().ById(default)));
        Assert.That(world.Reader().TryById(new EntityId(999), out Entity found), Is.False);
        Assert.That(found.IsNull, Is.True);
    }

    [Test]
    public void ById_DeletedId_ThrowsKeyNotFound()
    {
        World world = new WorldFactory().Create();
        Entity entity = world.Create();
        world.Delete(entity);

        Assert.Throws<KeyNotFoundException>(new Action(() => world.Reader().ById(entity.Id)));
        Assert.That(world.Reader().TryById(entity.Id, out _), Is.False);
    }

    [Test]
    public void Get_ReturnsACopy()
    {
        World world = new WorldFactory().Create();
        Entity entity = world.Create();
        world.Set(entity, new Position { X = 1, Y = 2 });

        Position position = world.Reader().Get<Position>(entity);
        position = position with { X = 9 };

        Assert.That(position.X, Is.EqualTo(9));
        Assert.That(world.Reader().Get<Position>(entity).X, Is.EqualTo(1));
    }

    [Test]
    public void Get_MissingComponent_ThrowsKeyNotFoundAndNamesIt()
    {
        World world = new WorldFactory().Create();
        Entity entity = world.Create();

        KeyNotFoundException e = Assert.Throws<KeyNotFoundException>(new Action(() => world.Reader().Get<Health>(entity)))!;

        Assert.That(e.Message, Does.Contain(nameof(Health)));
    }

    [Test]
    public void TryGet_ReturnsTheValueOrFalse()
    {
        World world = new WorldFactory().Create();
        Entity entity = world.Create();

        Assert.That(world.Reader().TryGet(entity, out Health missing), Is.False);
        Assert.That(missing.Value, Is.Zero);

        world.Set(entity, new Health { Value = 5 });

        Assert.That(world.Reader().TryGet(entity, out Health health), Is.True);
        Assert.That(health.Value, Is.EqualTo(5));
    }

    [Test]
    public void HasAndHasTag_NewEntity_AreFalse()
    {
        World world = new WorldFactory().Create();
        Entity entity = world.Create();

        Assert.That(world.Reader().Has<Health>(entity), Is.False);
        Assert.That(world.Reader().HasTag<CardTag>(entity), Is.False);
    }

    [Test]
    public void Component_CarriesAnEntityId()
    {
        World world = new WorldFactory().Create();
        Entity player = world.Create();
        Entity card = world.Create();

        world.Set(card, new Owner { Player = player.Id });

        Assert.That(world.Reader().ById(world.Reader().Get<Owner>(card).Player), Is.EqualTo(player));
    }
}
