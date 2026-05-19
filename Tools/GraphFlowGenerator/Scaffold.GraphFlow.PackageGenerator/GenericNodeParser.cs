using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using Microsoft.CodeAnalysis;

namespace Scaffold.GraphFlow.PackageGenerator
{
    /// <summary>
    /// Walks an assembly for hand-written <c>[GraphNode]</c> classes and projects them into
    /// <see cref="GenericNodeModel"/> records the <see cref="GraphGenericNodeEmitter"/> consumes.
    /// </summary>
    internal static class GenericNodeParser
    {
        const string GraphNodeAttrFqn = "Scaffold.GraphFlow.GraphNodeAttribute";
        const string GraphPortAttrFqn = "Scaffold.GraphFlow.GraphPortAttribute";

        internal static ImmutableArray<GenericNodeModel> Parse(Compilation compilation, IAssemblySymbol assembly, CancellationToken ct)
        {
            var allTypes = GraphPayloadTypeWalker.AllNamedTypesInAssembly(assembly, ct);
            return Parse(compilation, allTypes, ct);
        }

        internal static ImmutableArray<GenericNodeModel> Parse(Compilation compilation, ImmutableArray<INamedTypeSymbol> types, CancellationToken ct)
        {
            var graphNodeAttr = compilation.GetTypeByMetadataName(GraphNodeAttrFqn);
            if (graphNodeAttr == null)
            {
                return ImmutableArray<GenericNodeModel>.Empty;
            }

            var builder = ImmutableArray.CreateBuilder<GenericNodeModel>();
            foreach (var type in types)
            {
                ct.ThrowIfCancellationRequested();
                if (!HasGraphNodeAttribute(type, graphNodeAttr, out var category))
                {
                    continue;
                }

                if (type.TypeKind != TypeKind.Class || type.IsAbstract || type.DeclaredAccessibility != Accessibility.Public)
                {
                    continue;
                }

                if (type.TypeParameters.Length > 1)
                {
                    // Multi-T deferred. Skip silently.
                    continue;
                }

                var (_, isGenericOverRunner) = ClassifyHierarchy(type);

                // Skip generic types that are NOT generic-over-runner (e.g. Return<TResult>) —
                // their dynamic-options editor lands in phase 3. Until then, the runtime class
                // ships hand-written and the registry doesn't try to close them.
                if (type.TypeParameters.Length == 1 && !isGenericOverRunner)
                {
                    continue;
                }
                if (!DerivesFromRuntimeNodeBase(type) && !isGenericOverRunner)
                {
                    continue;
                }

                var (inputs, outputs, flowOuts, flowIns) = ParseFields(type);
                var hasInitHook = HasInitializePortsHook(type);

                // Flow-bearing iff the node has any flow port field. Replaces the old
                // `overrides Execute(Flow)` heuristic — Execute is gone, behavior lives on
                // FlowInPort handlers, so the field shape is the source of truth.
                var isFlowNode = flowIns.Length > 0 || flowOuts.Length > 0;

                var ns = type.ContainingNamespace.IsGlobalNamespace ? string.Empty : type.ContainingNamespace.ToDisplayString();
                builder.Add(new GenericNodeModel(
                    ns, type.Name, isFlowNode, isGenericOverRunner,
                    category, flowOuts, flowIns, inputs, outputs, hasInitHook));
            }

            return builder.ToImmutable();
        }

        static bool HasGraphNodeAttribute(INamedTypeSymbol type, INamedTypeSymbol graphNodeAttr,
            out string? category)
        {
            category = null;
            foreach (var a in type.GetAttributes())
            {
                if (!SymbolEqualityComparer.Default.Equals(a.AttributeClass, graphNodeAttr))
                {
                    continue;
                }

                foreach (var na in a.NamedArguments)
                {
                    if (na.Key == "Category" && na.Value.Value is string s)
                    {
                        category = s;
                    }
                }

                return true;
            }

            return false;
        }

        /// <summary>
        /// Walks the base chain. Returns (isFlowNode, isGenericOverRunner). Flow-bearing nodes derive
        /// either from <c>RuntimeNode`1</c> (typed runner) or override <c>Execute(Flow)</c> on
        /// <c>RuntimeNode</c> directly (runner-agnostic, post-M3 phase 2).
        /// </summary>
        static (bool isFlowNode, bool isGenericOverRunner) ClassifyHierarchy(INamedTypeSymbol type)
        {
            for (var current = type.BaseType; current != null; current = current.BaseType)
            {
                if (current.MetadataName == "RuntimeNode`1")
                {
                    return (true, type.TypeParameters.Length == 1);
                }
            }

            // Non-typed runtime node: check whether it overrides the virtual Execute(Flow).
            if (DerivesFromRuntimeNodeBase(type))
            {
                if (OverridesExecuteFlow(type))
                {
                    return (true, false);
                }
            }

            return (false, false);
        }

        static bool DerivesFromRuntimeNodeBase(INamedTypeSymbol type)
        {
            for (var current = type.BaseType; current != null; current = current.BaseType)
            {
                if (current.Name == "RuntimeNode" && !current.IsGenericType)
                {
                    return true;
                }
            }

            return false;
        }

        static bool OverridesExecuteFlow(INamedTypeSymbol type)
        {
            foreach (var member in type.GetMembers("Execute"))
            {
                if (member is not IMethodSymbol m) continue;
                if (m.Parameters.Length != 1) continue;
                if (m.Parameters[0].Type.Name != "Flow") continue;
                return true;
            }
            return false;
        }

        static (ImmutableArray<GenericNodeInputField> inputs,
                ImmutableArray<GenericNodeOutputField> outputs,
                ImmutableArray<GenericNodeFlowOut> flowOuts,
                ImmutableArray<GenericNodeFlowIn> flowIns) ParseFields(INamedTypeSymbol type)
        {
            var inputs = ImmutableArray.CreateBuilder<GenericNodeInputField>();
            var outputs = ImmutableArray.CreateBuilder<GenericNodeOutputField>();
            var flowOuts = ImmutableArray.CreateBuilder<GenericNodeFlowOut>();
            var flowIns = ImmutableArray.CreateBuilder<GenericNodeFlowIn>();

            foreach (var member in type.GetMembers())
            {
                if (member is not IFieldSymbol field || field.IsStatic)
                {
                    continue;
                }

                if (field.Type is not INamedTypeSymbol typed)
                {
                    continue;
                }

                if (typed.Name == "InputPort" && typed.TypeArguments.Length == 1)
                {
                    inputs.Add(new GenericNodeInputField(field.Name, TypeFmt.Simple(typed.TypeArguments[0])));
                }
                else if (typed.Name == "OutputPort" && typed.TypeArguments.Length == 1)
                {
                    outputs.Add(new GenericNodeOutputField(field.Name, TypeFmt.Simple(typed.TypeArguments[0])));
                }
                else if (typed.Name == "FlowOutPort")
                {
                    flowOuts.Add(new GenericNodeFlowOut(field.Name));
                }
                else if (typed.Name == "FlowInPort")
                {
                    flowIns.Add(new GenericNodeFlowIn(field.Name));
                }
            }

            return (inputs.ToImmutable(), outputs.ToImmutable(), flowOuts.ToImmutable(), flowIns.ToImmutable());
        }

        static bool HasInitializePortsHook(INamedTypeSymbol type)
        {
            foreach (var member in type.GetMembers("InitializePorts"))
            {
                if (member is IMethodSymbol m && m.IsPartialDefinition)
                {
                    return true;
                }
                if (member is IMethodSymbol m2 && m2.PartialImplementationPart != null)
                {
                    return true;
                }
            }

            foreach (var member in type.GetMembers("InitializePorts"))
            {
                if (member is IMethodSymbol)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
