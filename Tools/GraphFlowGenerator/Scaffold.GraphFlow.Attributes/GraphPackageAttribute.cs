using System;

namespace Scaffold.GraphFlow
{
    /// <summary>
    /// Declares one graph package (runner + bake/editor configuration). Apply at assembly scope only.
    /// <see cref="AttributeUsageAttribute.AllowMultiple"/> is <c>true</c> so multiple independent packages may coexist in one assembly.
    /// </summary>
    /// <remarks>
    /// <para><strong>Consumer requirements</strong> (the assembly that hosts this attribute):</para>
    /// <list type="number">
    /// <item><description>Reference the <c>Scaffold.GraphFlow.AttributesLib</c> assembly (Unity: the precompiled DLL wired through <c>Scaffold.GraphFlow.PackageAttributes.asmdef</c>).</description></item>
    /// <item><description>Reference the <c>Scaffold.GraphFlow.PackageGenerator</c> Roslyn analyzer so generation runs on this compilation (Unity: asmdef GUID reference to the generator DLL; labels: RoslynAnalyzer, RunOnlyOnAssembliesWithReference, explicit reference).</description></item>
    /// <item><description>Set <see cref="Runner"/> to a concrete, visible <c>GraphRunner</c> subclass type.</description></item>
    /// <item><description>For production emit: set <see cref="Extension"/>, <see cref="AssetMenu"/>, <see cref="Convention"/>, and <see cref="RegistryNamespace"/>; extension must not include a leading dot.</description></item>
    /// </list>
    /// <para><strong>Mode 2</strong> (wrap existing hierarchies): optionally set <see cref="CommandBase"/>, <see cref="EntryBase"/>, and helper bases such as <see cref="DispatcherBase"/> per ExecPlan v2.</para>
    /// </remarks>
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true, Inherited = false)]
    public sealed class GraphPackageAttribute : Attribute
    {
        /// <summary>Required — discriminates payloads and parameterizes generated Graph/Asset/Importer types.</summary>
        public Type Runner { get; set; } = null!;

        /// <summary>Required for full importer/menu wiring — file extension for Graph Toolkit source files (no leading dot; e.g. <c>mygraph</c>).</summary>
        public string Extension { get; set; } = "";

        /// <summary>Required for authoring UX — project window path for creating new graph assets.</summary>
        public string AssetMenu { get; set; } = "";

        /// <summary>Required for port discovery when emitting dispatcher/listener nodes — see <see cref="PortConvention"/>.</summary>
        public PortConvention Convention { get; set; }

        /// <summary>Required for emitted types — root namespace for generated registry and nodes.</summary>
        public string RegistryNamespace { get; set; } = "";

        /// <summary>Optional — Mode 2 entry payload base type.</summary>
        public Type? EntryBase { get; set; }

        /// <summary>Optional — Mode 2 command base (e.g. open generic <c>Command&lt;&gt;</c>).</summary>
        public Type? CommandBase { get; set; }

        /// <summary>Optional — Mode 2 generated dispatcher runtime base (closed per payload by the generator).</summary>
        public Type? DispatcherBase { get; set; }

        /// <summary>Optional — generated entry node editor/runtime base.</summary>
        public Type? EntryNodeBase { get; set; }

        /// <summary>Optional — generated listener node base.</summary>
        public Type? ListenerBase { get; set; }

        /// <summary>Optional — generated return-terminator node base.</summary>
        public Type? ReturnBase { get; set; }
    }
}
