#nullable enable
using System;
using Cysharp.Threading.Tasks;
using Scaffold.Variables;
using UnityEngine;

namespace Scaffold.GraphFlow.Nodes
{
    [Serializable]
    public sealed class ObserveVariable<T> : RuntimeNode
    {
        [SerializeField] string variableId = string.Empty;
        public FlowOutPort FlowOut = null!;
        public OutputPort<T> NewValue = null!;
        IVariableHandle<T>? _handle;

        public ObserveVariable()
        {
            FlowOut = new FlowOutPort(this, "FlowOut");
            NewValue = new OutputPort<T>(flow => PayloadOf(flow).Value);
            Ports.Add(FlowOut.Name, FlowOut);
            Ports.Add("NewValue", NewValue);
        }

        public override void Initialize(GraphRunner runner)
        {
            if (!runner.Variables.TryGet<T>(variableId, out _handle)) return;
            _handle.Subscribe(v => runner.RunObserver(FlowOut, new VariableChangePayload<T>(v)).Forget());
        }

        // Mirrors EntryRuntimeNode<TPayload>.PayloadOf — ObserveVariable is driven
        // by RunObserver with a typed VariableChangePayload<T>, so the cast is safe.
        static VariableChangePayload<T> PayloadOf(Flow flow) =>
            ((Flow<VariableChangePayload<T>>)flow).Payload;
    }
}
