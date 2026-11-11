using Cards.Domain.Shared;
using Ecs.Contracts.Commands;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Resources.Domain.Shared;
using Resources.Slices.ChangeResource;
using VSA.Composition;
using VSA.Handlers.Commands;
using VSA.Module;
using Wire.Handlers.Commands;

namespace Cards.Tests;

// IComponent and ITag have [TypeArgument], so each component and tag of a module is a key. The module scan closes the
// primitive handlers for each key, and Wire gives each closed primitive a catalog id. No module registers a key.
public sealed class ModuleKeyTests
{
    private static readonly Type[] ModulePrimitives =
    {
        typeof(ChangeComponent<Health>), typeof(RemoveComponent<Health>),
        typeof(ChangeComponent<Mana>), typeof(RemoveComponent<Mana>),
        typeof(ChangeComponent<CardZone>), typeof(RemoveComponent<CardZone>),
        typeof(AddTag<Card>), typeof(RemoveTag<Card>),
    };

    [Test]
    public void Catalog_HasThePrimitivesOfEachModuleKey_AfterTheBuiltIns()
    {
        using IHost host = VsaBuilder.Create().UseGenericHost().Build();
        WireCatalog catalog = host.Services.GetRequiredService<WireCatalog>();

        byte[] ids = ModulePrimitives.Select(catalog.IdOf).ToArray();

        Assert.That(ids.Distinct().Count(), Is.EqualTo(ModulePrimitives.Length));
        Assert.That(ids, Is.All.GreaterThanOrEqualTo(4));
    }

    [Test]
    public void Catalog_TwoHosts_GiveTheSameIds()
    {
        using IHost first = VsaBuilder.Create().UseGenericHost().Build();
        using IHost second = VsaBuilder.Create().UseGenericHost().Build();
        WireCatalog a = first.Services.GetRequiredService<WireCatalog>();
        WireCatalog b = second.Services.GetRequiredService<WireCatalog>();

        Assert.That(b.Count, Is.EqualTo(a.Count));
        Assert.That(ModulePrimitives.Select(b.IdOf), Is.EqualTo(ModulePrimitives.Select(a.IdOf)));
    }

    [Test]
    public void ChangeResource_IsClosedOnlyForIntValueKeys()
    {
        using IHost host = VsaBuilder.Create().UseGenericHost().Build();
        ModuleTypes types = host.Services.GetRequiredService<ModuleTypes>();

        Type[] commands = types.Implementations(typeof(CommandHandler<,>))
            .Select(h => h.Service.GetGenericArguments()[0])
            .Where(c => c.IsGenericType && c.GetGenericTypeDefinition() == typeof(ChangeResourceCommand<>))
            .ToArray();

        Assert.That(commands, Is.EquivalentTo(new[] { typeof(ChangeResourceCommand<Health>), typeof(ChangeResourceCommand<Mana>) }));
    }
}
