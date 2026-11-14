using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Xml.Linq;
using Ecs.Domain;
using Ecs.Handlers.Commands;
using Ecs.Infrastructure;

namespace Ecs.Tests;

// Only Plugins/Ecs uses Friflo packages. Friflo.Engine.ECS brings Friflo.Json.Fliox, so each check matches the prefix.
// A key implements a Friflo marker through the Ecs markers, so other assemblies can name the two markers and nothing else.
public sealed class FrifloBoundaryTests
{
    private const string Friflo = "Friflo.";

    private static readonly string[] Markers = { "Friflo.Engine.ECS.IComponent", "Friflo.Engine.ECS.ITag" };

    private static readonly string[] AppFolders = { "Source/App/", "Source/Plugins/", "Source/Modules/" };

    private static readonly string[] EcsAssemblies =
    {
        "Ecs", "Ecs.Domain", "Ecs.Domain.Shared", "Ecs.Infrastructure", "Ecs.Contracts.Commands", "Ecs.Handlers.Commands",
    };

    private static readonly string[] WorldOwners = Array.Empty<string>();

    [Test]
    public void B1_NoProjectOutsideEcsReferencesFriflo()
    {
        string root = RepoRoot();
        List<string> offenders = new List<string>();

        foreach (string folder in AppFolders)
        {
            string path = Path.Combine(root, folder);
            if (!Directory.Exists(path))
            {
                continue;
            }

            foreach (string project in Directory.EnumerateFiles(path, "*.csproj", SearchOption.AllDirectories))
            {
                string relative = Path.GetRelativePath(root, project).Replace('\\', '/');
                if (!relative.StartsWith("Source/Plugins/Ecs/", StringComparison.Ordinal) && ReferencesFriflo(project))
                {
                    offenders.Add(relative);
                }
            }
        }

        Assert.That(ReferencesFriflo(Path.Combine(root, "Source/Plugins/Ecs/Projects/Ecs.Domain.Shared.csproj")), Is.True);
        Assert.That(offenders, Is.Empty);
    }

    [Test]
    public void B2_NoAssemblyOutsideEcsUsesAFrifloTypeOtherThanTheMarkers()
    {
        List<string> offenders = new List<string>();

        foreach (string assembly in AppAssemblies().Where(a => !EcsAssemblies.Contains(a)))
        {
            offenders.AddRange(FrifloTypeReferences(assembly).Select(t => assembly + ": " + t));
        }

        Assert.That(FrifloTypeReferences("Ecs.Domain"), Is.Not.Empty);
        Assert.That(offenders, Is.Empty);
    }

    [Test]
    public void B3_TheEcsApiUsesNoFrifloTypeOtherThanTheMarkers()
    {
        List<string> offenders = new List<string>();

        foreach (string name in EcsAssemblies)
        {
            foreach (Type type in Assembly.Load(name).GetExportedTypes())
            {
                offenders.AddRange(PublicSurface(type).Where(IsForbiddenFrifloType).Select(t => type.Name + ": " + t.FullName));
            }
        }

        Assert.That(PublicSurface(typeof(Ecs.Domain.Shared.IComponent)).Select(t => t.FullName), Does.Contain(Markers[0]));
        Assert.That(PublicSurface(typeof(FrifloSurfaceProbe)).Where(IsForbiddenFrifloType), Is.Not.Empty);
        Assert.That(offenders, Is.Empty);
    }

    [Test]
    public void B4_TheReaderOfAWorldIsNotAWriter()
    {
        World world = new WorldFactory().Create();

        Assert.That(world.Reader() is IWorldWriter, Is.False);
    }

    // The owner of a match gives a reader to its scope. A field outside Ecs keeps it after the scope. WorldOwners
    // lists the types outside Ecs that may keep one, by full name. The match owner (MatchFactory, MatchWorld) is in Ecs.
    [Test]
    public void B5_NoTypeOutsideEcsKeepsAWorldInAField()
    {
        List<string> offenders = new List<string>();

        foreach (string name in AppAssemblies().Where(a => !EcsAssemblies.Contains(a)))
        {
            foreach (Type type in Assembly.Load(name).GetTypes().Where(t => !WorldOwners.Contains(t.FullName)))
            {
                offenders.AddRange(WorldFields(type).Select(f => type.FullName + "." + f));
            }
        }

        Assert.That(WorldFields(typeof(WorldFieldProbe)), Has.Count.EqualTo(4));
        Assert.That(offenders, Is.Empty);
    }

    private static List<string> WorldFields(Type type)
    {
        const BindingFlags Fields = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance
            | BindingFlags.Static | BindingFlags.DeclaredOnly;
        Type[] world = { typeof(World), typeof(IWorldWriter), typeof(IWorldReader), typeof(MatchWorld) };

        return type.GetFields(Fields)
            .Where(f => Unwrap(f.FieldType).Any(t => world.Contains(t)))
            .Select(f => f.Name)
            .ToList();
    }

    private static string RepoRoot()
    {
        DirectoryInfo? folder = new DirectoryInfo(AppContext.BaseDirectory);
        while (folder != null && !File.Exists(Path.Combine(folder.FullName, "CardFramework.slnx")))
        {
            folder = folder.Parent;
        }

        return folder?.FullName ?? throw new InvalidOperationException("CardFramework.slnx not found above the test output.");
    }

    private static bool ReferencesFriflo(string project)
    {
        return XDocument.Load(project).Descendants("PackageReference")
            .Any(r => ((string?)r.Attribute("Include") ?? string.Empty).StartsWith(Friflo, StringComparison.OrdinalIgnoreCase));
    }

    private static IEnumerable<string> AppAssemblies()
    {
        XDocument solution = XDocument.Load(Path.Combine(RepoRoot(), "CardFramework.slnx"));
        return solution.Descendants("Project")
            .Select(p => ((string)p.Attribute("Path")!).Replace('\\', '/'))
            .Where(p => AppFolders.Any(f => p.StartsWith(f, StringComparison.Ordinal)))
            .Select(Path.GetFileNameWithoutExtension)
            .Select(n => n!);
    }

    private static List<string> FrifloTypeReferences(string assembly)
    {
        string path = Path.Combine(AppContext.BaseDirectory, assembly + ".dll");
        Assert.That(File.Exists(path), Is.True, path);

        using FileStream stream = File.OpenRead(path);
        using PEReader pe = new PEReader(stream);
        MetadataReader reader = pe.GetMetadataReader();
        List<string> found = new List<string>();

        foreach (TypeReferenceHandle handle in reader.TypeReferences)
        {
            TypeReference type = reader.GetTypeReference(handle);
            if (!(OuterScopeName(reader, type) ?? string.Empty).StartsWith(Friflo, StringComparison.Ordinal))
            {
                continue;
            }

            string name = reader.GetString(type.Namespace) + "." + reader.GetString(type.Name);
            if (!Markers.Contains(name))
            {
                found.Add(name);
            }
        }

        return found;
    }

    private static string? OuterScopeName(MetadataReader reader, TypeReference type)
    {
        while (type.ResolutionScope.Kind == HandleKind.TypeReference)
        {
            type = reader.GetTypeReference((TypeReferenceHandle)type.ResolutionScope);
        }

        if (type.ResolutionScope.Kind != HandleKind.AssemblyReference)
        {
            return null;
        }

        AssemblyReference scope = reader.GetAssemblyReference((AssemblyReferenceHandle)type.ResolutionScope);
        return reader.GetString(scope.Name);
    }

    private static IEnumerable<Type> PublicSurface(Type type)
    {
        const BindingFlags Members = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance
            | BindingFlags.Static | BindingFlags.DeclaredOnly;
        List<Type> used = new List<Type>();

        if (type.BaseType != null)
        {
            used.Add(type.BaseType);
        }

        used.AddRange(type.GetInterfaces());
        used.AddRange(Constraints(type.IsGenericTypeDefinition ? type.GetGenericArguments() : Type.EmptyTypes));
        used.AddRange(type.GetFields(Members).Where(f => f.IsPublic || f.IsFamily || f.IsFamilyOrAssembly).Select(f => f.FieldType));
        used.AddRange(type.GetEvents(Members).Where(e => Visible(e.AddMethod)).Select(e => e.EventHandlerType!));
        used.AddRange(type.GetProperties(Members).Where(p => Visible(p.GetMethod) || Visible(p.SetMethod)).Select(p => p.PropertyType));
        foreach (MethodBase method in type.GetMethods(Members).Cast<MethodBase>().Concat(type.GetConstructors(Members)).Where(Visible))
        {
            used.AddRange(method.GetParameters().Select(p => p.ParameterType));
            if (method is MethodInfo info)
            {
                used.Add(info.ReturnType);
                used.AddRange(Constraints(info.IsGenericMethodDefinition ? info.GetGenericArguments() : Type.EmptyTypes));
            }
        }

        return used.SelectMany(Unwrap);
    }

    private static bool Visible(MethodBase? method)
    {
        return method != null && (method.IsPublic || method.IsFamily || method.IsFamilyOrAssembly);
    }

    private static IEnumerable<Type> Constraints(Type[] parameters)
    {
        return parameters.SelectMany(p => p.GetGenericParameterConstraints());
    }

    private static IEnumerable<Type> Unwrap(Type type)
    {
        if (type.HasElementType)
        {
            return Unwrap(type.GetElementType()!);
        }

        if (type.IsGenericType)
        {
            return new[] { type.GetGenericTypeDefinition() }.Concat(type.GetGenericArguments().SelectMany(Unwrap));
        }

        return new[] { type };
    }

    private static bool IsForbiddenFrifloType(Type type)
    {
        return (type.Assembly.GetName().Name ?? string.Empty).StartsWith(Friflo, StringComparison.Ordinal)
            && !Markers.Contains(type.FullName);
    }
}

public class FrifloSurfaceProbe
{
    protected void Leak<T>()
        where T : struct, Friflo.Engine.ECS.ILinkComponent
    {
    }
}

public class WorldFieldProbe
{
    private static World? shared;

    private readonly IWorldWriter writer;

    private readonly List<IWorldReader> readers;

    private readonly MatchWorld match;

    public WorldFieldProbe(World world, MatchWorld match)
    {
        shared = world;
        writer = world.Writer();
        readers = new List<IWorldReader> { world.Reader() };
        this.match = match;
    }

    public bool IsEmpty => shared == null && writer == null && readers.Count == 0 && match == null;
}
