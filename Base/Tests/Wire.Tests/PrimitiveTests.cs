using Ecs.Contracts.Commands;
using Ecs.Domain;
using Ecs.Domain.Shared;
using Microsoft.Extensions.Hosting;
using VSA.Contracts;
using Wire.Tests.Fixtures;

namespace Wire.Tests;

// Each primitive goes through the bus of a match scope: the handler writes the World of that scope,
// and a primitive that would not change the World comes back with an error.
public sealed class PrimitiveTests
{
    private IHost host = null!;
    private Match match = null!;

    [SetUp]
    public void Start()
    {
        host = Match.Host();
        match = new Match(host);
    }

    [TearDown]
    public void Stop()
    {
        match?.Dispose();
        host?.Dispose();
    }

    [Test]
    public async Task ChangeComponent_NewValue_WritesAndReturnsValue()
    {
        Entity entity = match.World.Create();

        Result<Health> result = await match.Send(new ChangeComponent<Health>(entity.Id, new Health { Value = 27 }));

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value.Value, Is.EqualTo(27));
        Assert.That(match.World.Get<Health>(entity).Value, Is.EqualTo(27));
    }

    [Test]
    public async Task ChangeComponent_EqualValue_ReturnsUnchanged()
    {
        Entity entity = match.World.Create();
        match.World.Set(entity, new Health { Value = 27 });

        Result<Health> result = await match.Send(new ChangeComponent<Health>(entity.Id, new Health { Value = 27 }));

        Assert.That(result.Error, Is.EqualTo(PrimitiveErrors.Unchanged));
    }

    [Test]
    public async Task ChangeComponent_MissingEntity_ReturnsEntityMissing()
    {
        Result<Health> result = await match.Send(new ChangeComponent<Health>(new EntityId(99), new Health { Value = 1 }));

        Assert.That(result.Error, Is.EqualTo(PrimitiveErrors.EntityMissing));
        Assert.That(match.World.Count, Is.Zero);
    }

    [Test]
    public async Task RemoveComponent_Present_Removes()
    {
        Entity entity = match.World.Create();
        match.World.Set(entity, new Health { Value = 27 });

        Result<Unit> result = await match.Send(new RemoveComponent<Health>(entity.Id));

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(match.World.Has<Health>(entity), Is.False);
    }

    [Test]
    public async Task RemoveComponent_Absent_ReturnsUnchanged()
    {
        Entity entity = match.World.Create();

        Result<Unit> result = await match.Send(new RemoveComponent<Health>(entity.Id));

        Assert.That(result.Error, Is.EqualTo(PrimitiveErrors.Unchanged));
    }

    [Test]
    public async Task AddTag_Absent_Adds()
    {
        Entity entity = match.World.Create();

        Result<Unit> result = await match.Send(new AddTag<Marked>(entity.Id));

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(match.World.HasTag<Marked>(entity), Is.True);
    }

    [Test]
    public async Task AddTag_Present_ReturnsUnchanged()
    {
        Entity entity = match.World.Create();
        match.World.AddTag<Marked>(entity);

        Result<Unit> result = await match.Send(new AddTag<Marked>(entity.Id));

        Assert.That(result.Error, Is.EqualTo(PrimitiveErrors.Unchanged));
    }

    [Test]
    public async Task RemoveTag_Present_Removes()
    {
        Entity entity = match.World.Create();
        match.World.AddTag<Marked>(entity);

        Result<Unit> result = await match.Send(new RemoveTag<Marked>(entity.Id));

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(match.World.HasTag<Marked>(entity), Is.False);
    }

    [Test]
    public async Task RemoveTag_Absent_ReturnsUnchanged()
    {
        Entity entity = match.World.Create();

        Result<Unit> result = await match.Send(new RemoveTag<Marked>(entity.Id));

        Assert.That(result.Error, Is.EqualTo(PrimitiveErrors.Unchanged));
    }

    [Test]
    public async Task CreateEntity_GivenId_CreatesThatId()
    {
        Result<EntityId> result = await match.Send(new CreateEntity(new EntityId(5)));

        Assert.That(result.Value, Is.EqualTo(new EntityId(5)));
        Assert.That(match.World.TryById(new EntityId(5), out _), Is.True);
    }

    [Test]
    public async Task CreateEntity_DefaultId_UsesNextId()
    {
        match.World.Create(new EntityId(5));

        Result<EntityId> result = await match.Send(new CreateEntity(default));

        Assert.That(result.Value, Is.EqualTo(new EntityId(6)));
    }

    [Test]
    public async Task CreateEntity_LiveId_ReturnsEntityExists()
    {
        match.World.Create(new EntityId(5));

        Result<EntityId> result = await match.Send(new CreateEntity(new EntityId(5)));

        Assert.That(result.Error, Is.EqualTo(PrimitiveErrors.EntityExists));
    }

    [Test]
    public async Task DeleteEntity_Live_Deletes()
    {
        Entity entity = match.World.Create();

        Result<Unit> result = await match.Send(new DeleteEntity(entity.Id));

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(match.World.TryById(entity.Id, out _), Is.False);
    }

    [Test]
    public async Task DeleteEntity_Missing_ReturnsEntityMissing()
    {
        Result<Unit> result = await match.Send(new DeleteEntity(new EntityId(99)));

        Assert.That(result.Error, Is.EqualTo(PrimitiveErrors.EntityMissing));
    }
}
