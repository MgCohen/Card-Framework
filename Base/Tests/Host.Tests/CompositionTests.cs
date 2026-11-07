using Host.Domain.Shared;
using Host.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Host.Tests;

public sealed class CompositionTests
{
    [Test]
    public void Build_resolves_the_services_of_each_module()
    {
        using var services = Composition.Build(PeerRole.Server, null, new CounterModule(), new ClockModule());

        Assert.That(services.GetService<Counter>(), Is.Not.Null);
        Assert.That(services.GetService<Clock>(), Is.Not.Null);
    }

    [TestCase(PeerRole.Server, typeof(ServerOnly), typeof(ClientOnly))]
    [TestCase(PeerRole.Client, typeof(ClientOnly), typeof(ServerOnly))]
    public void A_module_adds_the_services_of_its_role_only(PeerRole role, Type present, Type absent)
    {
        using var services = Composition.Build(role, null, new RoleModule());

        Assert.That(services.GetService(present), Is.Not.Null);
        Assert.That(services.GetService(absent), Is.Null);
    }

    [TestCase(PeerRole.Server)]
    [TestCase(PeerRole.Client)]
    public void PeerRole_resolves_and_a_constructor_takes_it(PeerRole role)
    {
        using var services = Composition.Build(role, null, new RoleReaderModule());

        Assert.That(services.GetRequiredService<PeerRole>(), Is.EqualTo(role));
        Assert.That(services.GetRequiredService<RoleReader>().Role, Is.EqualTo(role));
    }

    [Test]
    public void The_given_transport_resolves()
    {
        var transport = new SilentTransport();

        using var services = Composition.Build(PeerRole.Client, transport);

        Assert.That(services.GetService<ITransport>(), Is.SameAs(transport));
    }

    [Test]
    public void No_transport_is_registered_when_none_is_given()
    {
        using var services = Composition.Build(PeerRole.Client, null);

        Assert.That(services.GetService<ITransport>(), Is.Null);
    }

    [Test]
    public void The_modules_configure_in_the_given_order()
    {
        var log = new List<string>();

        using var services = Composition.Build(PeerRole.Server, null, new FirstLogModule("A", log), new SecondLogModule("B", log));

        Assert.That(log, Is.EqualTo(new[] { "A", "B" }));
    }

    [Test]
    public void Two_modules_of_the_same_type_throw()
    {
        Action build = () => Composition.Build(PeerRole.Server, null, new CounterModule(), new CounterModule());

        Assert.That(build, Throws.ArgumentException);
    }

    [Test]
    public void Two_calls_give_different_singletons()
    {
        using var server = Composition.Build(PeerRole.Server, null, new CounterModule());
        using var client = Composition.Build(PeerRole.Client, null, new CounterModule());

        Assert.That(server.GetRequiredService<Counter>(), Is.Not.SameAs(client.GetRequiredService<Counter>()));
    }

    [Test]
    public void A_missing_dependency_throws_in_Build()
    {
        Action build = () => Composition.Build(PeerRole.Server, null, new BrokenModule());

        Assert.That(build, Throws.TypeOf<AggregateException>());
    }

    [Test]
    public void A_null_module_throws()
    {
        Action build = () => Composition.Build(PeerRole.Server, null, new CounterModule(), null!);

        Assert.That(build, Throws.ArgumentException);
    }

    [Test]
    public void A_scoped_service_from_the_root_throws()
    {
        using var services = Composition.Build(PeerRole.Server, null, new ScopedModule());

        Action resolve = () => services.GetRequiredService<Clock>();

        Assert.That(resolve, Throws.InvalidOperationException);
    }

    sealed class Counter { }

    sealed class Clock { }

    sealed class ServerOnly { }

    sealed class ClientOnly { }

    sealed class RoleReader
    {
        public RoleReader(PeerRole role)
        {
            Role = role;
        }

        public PeerRole Role { get; }
    }

    sealed class NeedsCounter
    {
        public NeedsCounter(Counter counter)
        {
            Counter = counter;
        }

        public Counter Counter { get; }
    }

    sealed class CounterModule : IHostModule
    {
        public void Configure(IServiceCollection services, PeerRole role) => services.AddSingleton<Counter>();
    }

    sealed class ClockModule : IHostModule
    {
        public void Configure(IServiceCollection services, PeerRole role) => services.AddSingleton<Clock>();
    }

    sealed class RoleModule : IHostModule
    {
        public void Configure(IServiceCollection services, PeerRole role)
        {
            if (role == PeerRole.Server)
            {
                services.AddSingleton<ServerOnly>();
            }
            else
            {
                services.AddSingleton<ClientOnly>();
            }
        }
    }

    sealed class RoleReaderModule : IHostModule
    {
        public void Configure(IServiceCollection services, PeerRole role) => services.AddSingleton<RoleReader>();
    }

    sealed class ScopedModule : IHostModule
    {
        public void Configure(IServiceCollection services, PeerRole role) => services.AddScoped<Clock>();
    }

    sealed class BrokenModule : IHostModule
    {
        public void Configure(IServiceCollection services, PeerRole role) => services.AddSingleton<NeedsCounter>();
    }

    sealed class FirstLogModule : IHostModule
    {
        readonly string name;
        readonly List<string> log;

        public FirstLogModule(string name, List<string> log)
        {
            this.name = name;
            this.log = log;
        }

        public void Configure(IServiceCollection services, PeerRole role) => log.Add(name);
    }

    sealed class SecondLogModule : IHostModule
    {
        readonly string name;
        readonly List<string> log;

        public SecondLogModule(string name, List<string> log)
        {
            this.name = name;
            this.log = log;
        }

        public void Configure(IServiceCollection services, PeerRole role) => log.Add(name);
    }

    sealed class SilentTransport : ITransport
    {
        public event FrameHandler? Received
        {
            add { }
            remove { }
        }

        public void Send(ReadOnlySpan<byte> frame) { }
    }
}
