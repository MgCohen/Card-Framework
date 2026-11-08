using Ecs.Domain;
using Ecs.Tests.Fixtures;

namespace Ecs.Tests;

public sealed class AllocationTests
{
    private const int Calls = 10_000;

    [Test]
    public void Set_EqualValue_AllocatesNothing()
    {
        World world = new WorldFactory().Create();
        Entity entity = world.Create();
        world.Set(entity, new Position { X = 1, Y = 2 });

        long bytes = Measure(() =>
        {
            for (int i = 0; i < Calls; i++)
            {
                world.Set(entity, new Position { X = 1, Y = 2 });
            }
        });

        Assert.That(bytes, Is.Zero);
    }

    [Test]
    public void Set_ChangedValue_AllocatesNothing()
    {
        World world = new WorldFactory().Create();
        Entity entity = world.Create();
        world.Set<Health>(entity, 0);

        long bytes = Measure(() =>
        {
            for (int i = 0; i < Calls; i++)
            {
                world.Set<Health>(entity, i);
            }
        });

        Assert.That(bytes, Is.Zero);
    }

    [Test]
    public void GetAndHas_AllocateNothing()
    {
        World world = new WorldFactory().Create();
        Entity entity = world.Create();
        world.Set<Health>(entity, 3);
        IWorldReader reader = world.Reader;

        long bytes = Measure(() =>
        {
            for (int i = 0; i < Calls; i++)
            {
                _ = reader.Get<Health>(entity);
                _ = reader.Has<Health>(entity);
            }
        });

        Assert.That(bytes, Is.Zero);
    }

    // The first run warms up the JIT and the Friflo type caches; only the second run counts.
    private static long Measure(Action run)
    {
        run();
        long before = GC.GetAllocatedBytesForCurrentThread();
        run();
        return GC.GetAllocatedBytesForCurrentThread() - before;
    }
}
