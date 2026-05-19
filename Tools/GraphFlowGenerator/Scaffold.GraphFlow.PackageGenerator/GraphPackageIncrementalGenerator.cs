using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Scaffold.GraphFlow.PackageGenerator
{
    /// <summary>
    /// M1 incremental generator — reads <c>[assembly: GraphPackage(...)]</c> and emits Graph/Asset/Importer trio (ExecPlan v2).
    /// </summary>
    [Generator]
    public sealed class GraphPackageIncrementalGenerator : IIncrementalGenerator
    {
        const string graphPackageMetadataName = "Scaffold.GraphFlow.GraphPackageAttribute";

        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            var attrProvider = context.CompilationProvider.Select(static (c, _) => c.GetTypeByMetadataName(graphPackageMetadataName));
            var combined = attrProvider.Combine(context.CompilationProvider);
            var payload = combined.Select(static (t, ct) => (Packages: GraphPackageAssemblyParser.ParsePackages(t.Right, t.Left, ct), Compilation: t.Right));
            context.RegisterSourceOutput(payload, static (spc, x) => GraphPackageEmitter.Emit(spc, x.Compilation, x.Packages, spc.CancellationToken));
        }
    }
}
