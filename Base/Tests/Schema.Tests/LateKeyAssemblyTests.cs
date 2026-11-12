using System.Runtime.CompilerServices;
using Cards.Domain.Shared;
using Cards.Slices.CreateCard;
using Ecs.Domain;
using Ecs.Domain.Shared;
using Ecs.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Resources.Domain.Shared;
using Resources.Slices.ChangeResource;
using VSA.Composition;
using VSA.Contracts.Commands;

namespace Schema.Tests;

// Friflo reads the loaded assemblies when it makes the first World. A key assembly that loads after that fails
// with TypeInitializationException. The module scan in Build() loads each layer assembly, so it loads each key
// assembly before MatchFactory can make a World. The test method itself names no key type: the JIT loads the types
// of a method before the method runs, so the key code is in Write, which the JIT compiles only when it is called.
public sealed class LateKeyAssemblyTests
{
    private static readonly string[] KeyAssemblies = { "Cards.Domain.Shared", "Resources.Domain.Shared" };

    [Test]
    public async Task KeyAssemblies_LoadInBuild_BeforeTheFirstWorld()
    {
        Assert.That(Loaded(), Is.Empty, "A key assembly loaded before Build. The test cannot show a late load.");

        using IHost host = VsaBuilder.Create().UseGenericHost().Build();

        Assert.That(Loaded(), Is.EquivalentTo(KeyAssemblies));
        Assert.That(await Write(host), Is.EqualTo("Board 7"));
    }

    private static string[] Loaded()
    {
        return AppDomain.CurrentDomain.GetAssemblies()
            .Select(a => a.GetName().Name ?? string.Empty)
            .Where(KeyAssemblies.Contains)
            .ToArray();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static async Task<string> Write(IHost host)
    {
        using IServiceScope match = host.Services.GetRequiredService<MatchFactory>().Create();
        ICommandSender commands = match.ServiceProvider.GetRequiredService<ICommandSender>();
        CreateCardCommand create = new CreateCardCommand(Zone.Board);
        EntityId card = (await commands.SendAsync(create, CancellationToken.None)).Value;
        await commands.SendAsync(new ChangeResourceCommand<Health>(card, 7), CancellationToken.None);

        IWorldReader world = match.ServiceProvider.GetRequiredService<IWorldReader>();
        Entity entity = world.ById(card);
        return $"{world.Get<CardZone>(entity).Value} {world.Get<Health>(entity).Value}";
    }
}
