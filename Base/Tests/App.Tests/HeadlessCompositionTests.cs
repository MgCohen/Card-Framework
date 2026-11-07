using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TypeKeys;
using TypeKeys.Domain;
using TypeKeys.Fixtures;
using VSA.Composition;
using VSA.Module;

namespace App.Tests;

// The VSA composer registers the modules with no web host, as a headless app or a peer does.
public sealed class HeadlessCompositionTests
{
    [Test]
    public void The_composer_registers_the_TypeKeys_registries_with_no_web_host()
    {
        ServiceCollection services = new ServiceCollection();
        IConfiguration configuration = new ConfigurationBuilder().Build();
        VsaComposer composer = new VsaComposer(new MicrosoftServices(services), configuration, new TypeKeysModule());

        composer.Install();

        using ServiceProvider provider = services.BuildServiceProvider();
        SuitRegistry suits = provider.GetRequiredService<SuitRegistry>();
        Assert.That(provider.GetRequiredService<TypeKeyRegistry<Suit>>(), Is.SameAs(suits));
        Assert.That(suits.Members, Is.EqualTo(new Suit[] { new Hearts(), new Spades() }));
    }
}
