using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using VSA.Contracts.Commands;

namespace App.Tests;

// The host starts and composes the VSA services, with the modules that it finds.
public sealed class HostTests
{
    [Test]
    public void The_host_starts()
    {
        using WebApplicationFactory<Program> factory = new WebApplicationFactory<Program>();

        Action start = () => _ = factory.Services;

        Assert.That(start, Throws.Nothing);
    }

    [Test]
    public void The_host_registers_the_VSA_command_sender()
    {
        using WebApplicationFactory<Program> factory = new WebApplicationFactory<Program>();
        using IServiceScope scope = factory.Services.CreateScope();

        Assert.That(scope.ServiceProvider.GetService<ICommandSender>(), Is.Not.Null);
    }
}
