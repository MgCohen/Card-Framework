#nullable enable
using System;
using Scaffold.Variables;
using UnityEngine;

namespace Scaffold.GraphFlow.Nodes
{
    [Serializable]
    public sealed class GetVariable<T> : RuntimeNode
    {
        [SerializeField] string variableId = string.Empty;
        public OutputPort<T> Value = null!;
        IVariableHandle<T>? _handle;

        public GetVariable()
        {
            Value = new OutputPort<T>(_ => _handle != null ? _handle.Value : default!, cache: false);
            Ports.Add("Value", Value);
        }

        public override void Initialize(GraphRunner runner) =>
            runner.Variables.TryGet<T>(variableId, out _handle);
    }
}
