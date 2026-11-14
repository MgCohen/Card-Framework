using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using TypeKeys.Domain;
using TypeKeys.Domain.Shared;
using TypeKeys.Fixtures;
using VSA.Testing.Architecture;

namespace App.Tests;

// The host finds the TypeKeys module, registers each key root, and skips the roots of a test assembly.
public sealed class TypeKeysHostTests
{
    [Test]
    public void The_host_finds_the_TypeKeys_module()
    {
        Assert.That(VsaApp.Current().Modules.Select(m => m.Name), Does.Contain("TypeKeys"));
    }

    [Test]
    public void The_host_registers_one_frozen_registry_for_a_key_root()
    {
        using WebApplicationFactory<Program> factory = new WebApplicationFactory<Program>();

        SuitRegistry suits = factory.Services.GetRequiredService<SuitRegistry>();

        Assert.That(factory.Services.GetRequiredService<TypeKeyRegistry<Suit>>(), Is.SameAs(suits));
        Assert.That(suits.Members, Is.EqualTo(new Suit[] { new Hearts(), new Spades() }));
        Action register = () => suits.Register<Hearts>();
        Assert.That(register, Throws.InvalidOperationException);
    }

    [Test]
    public void The_host_registers_no_registry_for_a_test_key_root()
    {
        using WebApplicationFactory<Program> factory = new WebApplicationFactory<Program>();

        Assert.That(factory.Services.GetService<TypeKeyRegistry<HostKey>>(), Is.Null);
    }
}

public abstract record HostKey : TypeKey
{
    protected HostKey(int slot) : base(slot)
    {
    }
}

public sealed record FirstHostKey() : HostKey(1);
