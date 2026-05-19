using System;
using System.Reflection;
using Unity.GraphToolkit.Editor;

namespace Scaffold.GraphFlow.Editor
{
    static class EditorNodeIdentity
    {
        static readonly FieldInfo s_NodeImplementation =
            typeof(Node).GetField("m_Implementation", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException(
                "GraphFlow: GraphToolkit Node.m_Implementation field not found — version drift. " +
                "Update the reflection contract in EditorNodeIdentity.");

        internal static string GetStableGuid(INode node)
        {
            if (node is not Node toolkitNode)
                throw new InvalidOperationException(
                    $"GraphFlow: editor node {node.GetType().FullName} does not derive from Unity.GraphToolkit.Editor.Node.");

            var impl = s_NodeImplementation.GetValue(toolkitNode)
                ?? throw new InvalidOperationException(
                    $"GraphFlow: Node.m_Implementation is null on {node.GetType().FullName}.");

            var guidProp = impl.GetType().GetProperty("Guid", BindingFlags.Public | BindingFlags.Instance)
                ?? throw new InvalidOperationException(
                    "GraphFlow: GraphToolkit Implementation.Guid property not found — version drift.");

            var s = guidProp.GetValue(impl)?.ToString();
            if (string.IsNullOrWhiteSpace(s))
                throw new InvalidOperationException(
                    $"GraphFlow: Implementation.Guid was empty for {node.GetType().FullName}.");

            return s;
        }
    }
}
