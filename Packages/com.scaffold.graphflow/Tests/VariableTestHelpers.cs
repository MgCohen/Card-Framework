#nullable enable
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Scaffold.Variables;

namespace Scaffold.GraphFlow.Tests
{
    // Shared test helpers to avoid duplicating IntRecorder / SetVariableId /
    // RuntimeVariable construction across the variable test suites.
    internal static class VariableTestHelpers
    {
        // Reflection helper: writes the [SerializeField] private 'variableId' field
        // on Get/Set/Observe nodes. Unity normally fills it from serialized YAML, but
        // tests construct nodes in code and need to poke it. One reflective call per
        // node init, never on hot path.
        public static void SetVariableId(RuntimeNode node, string id)
        {
            var field = node.GetType().GetField("variableId",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(field, $"Reflection: 'variableId' field not found on {node.GetType().Name}.");
            field!.SetValue(node, id);
        }

        // Constructs a RuntimeVariable from a typed default with the AssemblyQualifiedName
        // null-forgiving call colocated here (Type.AssemblyQualifiedName is `string?`
        // under nullable annotations; provably non-null for compile-time typeof()).
        public static RuntimeVariable Var(string id, VariableDefault def) => new()
        {
            id = id,
            name = id,
            typeName = def.ValueType.AssemblyQualifiedName!,
            defaultValue = def,
        };
    }

    // Records every value that arrives at its In port on the flow that ran it.
    // Used by Observe / end-to-end tests to assert the flow fired with the
    // expected new value(s).
    [System.Serializable]
    public sealed class IntRecorder : RuntimeNode
    {
        public FlowInPort In = null!;
        public InputPort<int> Value = null!;
        public readonly List<int> Recorded = new();

        public IntRecorder()
        {
            Value = new InputPort<int>();
            In = FlowInPort.Sync(this, nameof(In), flow =>
            {
                Recorded.Add(Value.Read(flow));
                return null;
            });
            Ports.Add(In.Name, In);
            Ports.Add(nameof(Value), Value);
        }
    }
}
