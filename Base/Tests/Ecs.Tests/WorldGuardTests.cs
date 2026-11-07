using Ecs.Domain;
using Ecs.Tests.Fixtures;

namespace Ecs.Tests;

public sealed class WorldGuardTests
{
    [Test]
    public void Writer_EntityOfAnotherWorld_Throws()
    {
        World first = new World();
        World second = new World();
        Entity entity = first.Create();

        Assert.Throws<InvalidOperationException>(new Action(() => second.Set<Health>(entity, 1)));
        Assert.Throws<InvalidOperationException>(new Action(() => second.AddTag<CardTag>(entity)));
        Assert.Throws<InvalidOperationException>(new Action(() => second.Delete(entity)));
    }

    [Test]
    public void Reader_EntityOfAnotherWorld_Throws()
    {
        World first = new World();
        World second = new World();
        Entity entity = first.Create();

        Assert.Throws<InvalidOperationException>(new Action(() => second.Reader.Has<Health>(entity)));
        Assert.Throws<InvalidOperationException>(new Action(() => second.Reader.Get<Health>(entity)));
    }

    [Test]
    public void ReadAndWrite_NullEntity_Throws()
    {
        World world = new World();
        Entity entity = default;

        Assert.Throws<InvalidOperationException>(new Action(() => world.Set<Health>(entity, 1)));
        Assert.Throws<InvalidOperationException>(new Action(() => world.Reader.Has<Health>(entity)));
    }

    [Test]
    public void ReadAndWrite_DeletedEntity_Throws()
    {
        World world = new World();
        Entity entity = world.Create();
        world.Delete(entity);

        Assert.Throws<InvalidOperationException>(new Action(() => world.Reader.TryGet(entity, out Health _)));
        Assert.Throws<InvalidOperationException>(new Action(() => world.Remove<Health>(entity)));
        Assert.Throws<InvalidOperationException>(new Action(() => world.RemoveTag<CardTag>(entity)));
    }

    [Test]
    public void Reader_IsNotAWriter()
    {
        World world = new World();

        Assert.That(world.Reader, Is.Not.InstanceOf<IWorldWriter>());
        Assert.That(world.Reader, Is.Not.InstanceOf<World>());
    }
}
