using System.Reflection;
using System.Runtime.CompilerServices;
using Ecs.Domain;
using Ecs.Infrastructure;

namespace Ecs.Tests;

// IWorldWriter is public. Only Ecs code can make a writer instance: World has no public members, the adapters are
// internal, and only the listed assemblies see the internal members.
public sealed class WriterGuardTests
{
    [Test]
    public void World_HasNoPublicMembers()
    {
        const BindingFlags Public = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

        Assert.That(typeof(World).GetMembers(Public), Is.Empty);
    }

    [TestCase("Ecs.Infrastructure.WorldWriter")]
    [TestCase("Ecs.Infrastructure.WorldReader")]
    [TestCase("Ecs.Infrastructure.WorldAccess")]
    public void Adapter_IsNotPublic(string name)
    {
        Type adapter = typeof(IWorldWriter).Assembly.GetType(name, throwOnError: true)!;

        Assert.That(adapter.IsPublic, Is.False);
    }

    [Test]
    public void EcsDomain_GivesItsInternalsOnlyToTheListedAssemblies()
    {
        Assert.That(InternalsVisibleTo(typeof(World).Assembly), Is.EquivalentTo(new[] { "Ecs.Infrastructure", "Ecs.Tests", "Wire.Tests" }));
    }

    [Test]
    public void EcsInfrastructure_GivesItsInternalsOnlyToTheListedAssemblies()
    {
        Assert.That(InternalsVisibleTo(typeof(IWorldWriter).Assembly), Is.EquivalentTo(new[] { "Ecs", "Ecs.Handlers.Commands", "Ecs.Tests" }));
    }

    private static IEnumerable<string> InternalsVisibleTo(Assembly assembly)
    {
        return assembly.GetCustomAttributes<InternalsVisibleToAttribute>().Select(a => a.AssemblyName);
    }
}
