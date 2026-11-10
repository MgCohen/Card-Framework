using System.Reflection;
using System.Xml.Linq;
using Ecs.Contracts.Commands;
using VSA.Handlers;
using Wire.Tests.Fixtures;

namespace Wire.Tests;

// B6: only Ecs, Wire and Trace hook a primitive. A validator, a Before or After event, a step or a handler on a
// primitive in another module could change or stop a World write, or react to it. A hook on a supertype,
// for example After<IMessage>, also runs on each primitive, so the check also uses probe primitives.
public sealed class PrimitiveBoundaryTests
{
    private static readonly string[] AppFolders = { "App/", "Plugins/", "Modules/" };

    // Trace hooks each message to record it. Trace.Tests (TraceHookTests) checks that each Trace hook only reads.
    private static readonly string[] Owners = { "Ecs", "Wire", "Trace" };

    private static readonly Type[] Probes =
    {
        typeof(CreateEntity), typeof(DeleteEntity), typeof(ChangeComponent<Health>), typeof(AddTag<Marked>),
    };

    private static readonly Type[] HookBases =
    {
        typeof(Validator<>), typeof(Before<>), typeof(After<>), typeof(BeforeStep<>), typeof(AfterStep<>),
    };

    [Test]
    public void B6_NoAssemblyOutsideEcsWireAndTraceHooksAPrimitive()
    {
        List<string> found = new List<string>();
        string[] others = AppAssemblies().Where(a => !Owners.Any(o => a == o || a.StartsWith(o + ".", StringComparison.Ordinal))).ToArray();

        Assert.That(others, Does.Contain("App.Host"));
        foreach (string name in others)
        {
            found.AddRange(Assembly.Load(name).GetTypes().Where(HooksAPrimitive).Select(t => $"{name}: {t.FullName}"));
        }

        Assert.That(found, Is.Empty);
    }

    [Test]
    public void B6_TheCheckFindsAHook()
    {
        Assert.That(HooksAPrimitive(typeof(HealthSpy)), Is.True);
        Assert.That(HooksAPrimitive(typeof(MessageSpy)), Is.True);
        Assert.That(HooksAPrimitive(typeof(PrimitiveTests)), Is.False);
    }

    private static bool HooksAPrimitive(Type type)
    {
        IEnumerable<Type> messages = Bases(type)
            .Where(b => b.IsGenericType && HookBases.Contains(b.GetGenericTypeDefinition()))
            .Concat(type.GetInterfaces().Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IHandler<>)))
            .Select(b => b.GetGenericArguments()[0]);

        return messages.Any(m => typeof(IPrimitive).IsAssignableFrom(m)
            || (m.IsGenericType && typeof(IPrimitive).IsAssignableFrom(m.GetGenericTypeDefinition()))
            || Probes.Any(m.IsAssignableFrom));
    }

    private static IEnumerable<Type> Bases(Type type)
    {
        for (Type? b = type.BaseType; b != null; b = b.BaseType)
        {
            yield return b;
        }
    }

    private static IEnumerable<string> AppAssemblies()
    {
        DirectoryInfo? folder = new DirectoryInfo(AppContext.BaseDirectory);
        while (folder != null && !File.Exists(Path.Combine(folder.FullName, "Base.slnx")))
        {
            folder = folder.Parent;
        }

        string root = folder?.FullName ?? throw new InvalidOperationException("Base.slnx not found above the test output.");
        return XDocument.Load(Path.Combine(root, "Base.slnx")).Descendants("Project")
            .Select(p => ((string)p.Attribute("Path")!).Replace('\\', '/'))
            .Where(p => AppFolders.Any(f => p.StartsWith(f, StringComparison.Ordinal)))
            .Select(p => Path.GetFileNameWithoutExtension(p)!);
    }
}
