#nullable enable
using System;
using Scaffold.Variables;
using UnityEngine;

namespace Scaffold.GraphFlow.Nodes
{
    [Serializable]
    public sealed class SetVariable<T> : RuntimeNode
    {
        [SerializeField] string variableId = string.Empty;
        public InputPort<T> NewValue = null!;
        public FlowInPort In = null!;
        public FlowOutPort Done = null!;
        IVariableHandle<T>? _handle;

        public SetVariable()
        {
            NewValue = new InputPort<T>();
            Done = new FlowOutPort(this, "Done");
            In = FlowInPort.Sync(this, nameof(In), flow =>
            {
                _handle?.Set(NewValue.Read(flow));
                return Done;
            });
            Ports.Add("NewValue", NewValue);
            Ports.Add(In.Name, In);
            Ports.Add(Done.Name, Done);
        }

        public override void Initialize(GraphRunner runner) =>
            runner.Variables.TryGet<T>(variableId, out _handle);
    }
}
