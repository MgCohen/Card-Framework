using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TypeKeys;
using TypeKeys.Domain;
using TypeKeys.Fixtures;
using VSA.Composition;

namespace App.Tests;

// The generic host registers the modules with no web host, as a headless app or a peer does.
public sealed class HeadlessCompositionTests
{
    [Test]
    public void The_generic_host_registers_the_TypeKeys_registries_with_no_web_host()
    {
        using IHost host = VsaBuilder.Create().UseGenericHost().Modules(new TypeKeysPlugin()).Build();

        SuitRegistry suits = host.Services.GetRequiredService<SuitRegistry>();
        Assert.That(host.Services.GetRequiredService<TypeKeyRegistry<Suit>>(), Is.SameAs(suits));
        Assert.That(suits.Members, Is.EqualTo(new Suit[] { new Hearts(), new Spades() }));
    }
}
