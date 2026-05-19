using System.Collections.Immutable;

namespace Scaffold.GraphFlow.PackageGenerator
{
    /// <summary>One typed input field on a <c>[GraphNode]</c> class.</summary>
    internal readonly struct GenericNodeInputField
    {
        internal GenericNodeInputField(string fieldName, string csharpType)
        {
            FieldName = fieldName;
            CSharpType = csharpType;
        }

        internal string FieldName { get; }
        /// <summary>Display string for the type argument T in <c>InputPort&lt;T&gt;</c>.</summary>
        internal string CSharpType { get; }
    }

    /// <summary>One typed output field on a <c>[GraphNode]</c> class.</summary>
    internal readonly struct GenericNodeOutputField
    {
        internal GenericNodeOutputField(string fieldName, string csharpType)
        {
            FieldName = fieldName;
            CSharpType = csharpType;
        }

        internal string FieldName { get; }
        internal string CSharpType { get; }
    }

    /// <summary>One <c>FlowOutPort</c> field on a <c>[GraphNode]</c> class.</summary>
    internal readonly struct GenericNodeFlowOut
    {
        internal GenericNodeFlowOut(string fieldName)
        {
            FieldName = fieldName;
        }

        internal string FieldName { get; }
    }

    /// <summary>One <c>FlowInPort</c> field on a <c>[GraphNode]</c> class. The field name IS the
    /// port name on every layer (editor mirror, registry, runtime <c>Ports</c> dict).</summary>
    internal readonly struct GenericNodeFlowIn
    {
        internal GenericNodeFlowIn(string fieldName)
        {
            FieldName = fieldName;
        }

        internal string FieldName { get; }
    }

    /// <summary>
    /// Parsed shape of a hand-written <c>RuntimeNode</c> (data) or <c>RuntimeNode&lt;TRunner&gt;</c>
    /// (flow) annotated with <c>[GraphNode]</c>. The generator emits a partial completing the runtime
    /// (default ctor that constructs port handles + populates the <c>Ports</c> dict) plus the editor
    /// mirror + registry entry per package whose runner closes <c>TRunner</c>.
    ///
    /// <para>Post-M3 phase 2 (decision #5): nodes can now derive from RuntimeNode (non-generic flow,
    /// runner-agnostic Execute) as well — IsGenericOverRunner=false, IsFlowNode=true means the node
    /// extends RuntimeNode and overrides the virtual Execute(Flow).</para>
    /// </summary>
    internal readonly struct GenericNodeModel
    {
        internal GenericNodeModel(
            string typeNamespace,
            string typeName,
            bool isFlowNode,
            bool isGenericOverRunner,
            string? category,
            ImmutableArray<GenericNodeFlowOut> flowOuts,
            ImmutableArray<GenericNodeFlowIn> flowIns,
            ImmutableArray<GenericNodeInputField> inputs,
            ImmutableArray<GenericNodeOutputField> outputs,
            bool hasInitializePortsHook)
        {
            TypeNamespace = typeNamespace;
            TypeName = typeName;
            IsFlowNode = isFlowNode;
            IsGenericOverRunner = isGenericOverRunner;
            Category = category;
            FlowOuts = flowOuts;
            FlowIns = flowIns;
            Inputs = inputs;
            Outputs = outputs;
            HasInitializePortsHook = hasInitializePortsHook;
        }

        internal string TypeNamespace { get; }
        internal string TypeName { get; }
        /// <summary>True when the node extends a flow-bearing base (overrides Execute). False for pure data nodes.</summary>
        internal bool IsFlowNode { get; }
        /// <summary>True for <c>Foo&lt;TRunner&gt;</c>; false for non-generic <c>Foo</c>.</summary>
        internal bool IsGenericOverRunner { get; }
        internal string? Category { get; }
        internal ImmutableArray<GenericNodeFlowOut> FlowOuts { get; }
        /// <summary>FlowInPort fields — one per declared FlowIn. Field name IS port name (R14
        /// — generator threads it through to the editor mirror + registry).</summary>
        internal ImmutableArray<GenericNodeFlowIn> FlowIns { get; }
        internal bool HasFlowIn => FlowIns.Length > 0;
        internal ImmutableArray<GenericNodeInputField> Inputs { get; }
        internal ImmutableArray<GenericNodeOutputField> Outputs { get; }
        /// <summary>True if the class declares <c>partial void InitializePorts()</c> with a body — the generated ctor calls it after constructing inputs.</summary>
        internal bool HasInitializePortsHook { get; }

        internal string FullyQualifiedNoGeneric =>
            string.IsNullOrEmpty(TypeNamespace) ? TypeName : TypeNamespace + "." + TypeName;
    }
}
