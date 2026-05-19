using Unity.GraphToolkit.Editor;

namespace Scaffold.GraphFlow.Editor.GToolkit
{
    /// <summary>
    /// Non-generic anchor for cross-asm node discovery via <see cref="UseWithGraphAttribute"/>.
    /// <para>GraphToolkit's <c>UseWithGraphAttribute.IsGraphTypeSupported</c> uses
    /// <c>Type.IsAssignableFrom</c>, which doesn't unify open generics with their closed forms.
    /// Tagging a shared-package node with <c>[UseWithGraph(typeof(Graph&lt;&gt;))]</c> would not
    /// match any consumer's <c>FooGraph : Graph&lt;FooRunner&gt;</c>. Routing through this
    /// non-generic intermediate fixes that — every consumer's Graph derives through
    /// <see cref="GraphFlowGraph"/>, so a single
    /// <c>[UseWithGraph(typeof(GraphFlowGraph))]</c> exposes the decorated node in every
    /// downstream package's Add Node menu.</para>
    /// </summary>
    public abstract class GraphFlowGraph : Unity.GraphToolkit.Editor.Graph
    {
    }

    /// <summary>
    /// Package base for generator-emitted graph subclasses — validates GT accepts generic inheritance.
    /// </summary>
    public abstract class Graph<TRunner> : GraphFlowGraph
        where TRunner : Scaffold.GraphFlow.GraphRunner
    {
        public abstract GraphPackageRegistry<TRunner> Registry { get; }
    }
}
