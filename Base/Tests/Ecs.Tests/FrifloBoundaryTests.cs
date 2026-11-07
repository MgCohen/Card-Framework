using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Xml.Linq;
using Ecs.Domain;

namespace Ecs.Tests;

// Only Shared/Ecs uses Friflo.Engine.ECS. A key implements a Friflo marker through the Ecs markers, so other
// assemblies can name the two markers and nothing else.
public sealed class FrifloBoundaryTests
{
    private const string Friflo = "Friflo.Engine.ECS";

    private static readonly string[] Markers = { Friflo + ".IComponent", Friflo + ".ITag" };

    private static readonly string[] AppFolders = { "App/", "Shared/", "Modules/" };

    private static readonly string[] EcsAssemblies = { "Ecs", "Ecs.Domain", "Ecs.Domain.Shared" };

    [Test]
    public void B1_NoProjectOutsideEcsReferencesFriflo()
    {
        string root = BaseRoot();
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
                if (!relative.StartsWith("Shared/Ecs/", StringComparison.Ordinal) && ReferencesFriflo(project))
                {
                    offenders.Add(relative);
                }
            }
        }

        Assert.That(ReferencesFriflo(Path.Combine(root, "Shared/Ecs/Projects/Ecs.Domain.Shared.csproj")), Is.True);
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

        Assert.That(offenders, Is.Empty);
    }

    [Test]
    public void B4_TheReaderOfAWorldIsNotAWriter()
    {
        World world = new World();

        Assert.That(world.Reader is IWorldWriter, Is.False);
    }

    private static string BaseRoot()
    {
        DirectoryInfo? folder = new DirectoryInfo(AppContext.BaseDirectory);
        while (folder != null && !File.Exists(Path.Combine(folder.FullName, "Base.slnx")))
        {
            folder = folder.Parent;
        }

        return folder?.FullName ?? throw new InvalidOperationException("Base.slnx not found above the test output.");
    }

    private static bool ReferencesFriflo(string project)
    {
        return XDocument.Load(project).Descendants("PackageReference")
            .Any(r => string.Equals((string?)r.Attribute("Include"), Friflo, StringComparison.OrdinalIgnoreCase));
    }

    private static IEnumerable<string> AppAssemblies()
    {
        XDocument solution = XDocument.Load(Path.Combine(BaseRoot(), "Base.slnx"));
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
            if (OuterScopeName(reader, type) != Friflo)
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
        const BindingFlags Members = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
        List<Type> used = new List<Type>();

        if (type.BaseType != null)
        {
            used.Add(type.BaseType);
        }

        used.AddRange(type.GetInterfaces());
        used.AddRange(type.GetFields(Members).Select(f => f.FieldType));
        used.AddRange(type.GetProperties(Members).Select(p => p.PropertyType));
        used.AddRange(type.GetEvents(Members).Select(e => e.EventHandlerType!));
        foreach (MethodBase method in type.GetMethods(Members).Cast<MethodBase>().Concat(type.GetConstructors()))
        {
            used.AddRange(method.GetParameters().Select(p => p.ParameterType));
            if (method is MethodInfo info)
            {
                used.Add(info.ReturnType);
            }
        }

        return used.SelectMany(Unwrap);
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
        return type.Assembly.GetName().Name == Friflo && !Markers.Contains(type.FullName);
    }
}
