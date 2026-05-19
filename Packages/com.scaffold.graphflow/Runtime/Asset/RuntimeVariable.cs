using System;
using Scaffold.Variables;
using UnityEngine;

namespace Scaffold.GraphFlow
{
    // Class, not struct: Unity's [SerializeReference] is ignored on fields of
    // value types, so a polymorphic VariableDefault inside a struct would
    // silently fail to serialize. https://docs.unity3d.com/ScriptReference/SerializeReference.html
    [Serializable]
    public sealed class RuntimeVariable
    {
        public string id = string.Empty;
        public string name = string.Empty;
        public string typeName = string.Empty;
        [SerializeReference] public VariableDefault defaultValue;
    }

    [Serializable]
    public struct VariableEdge
    {
        public string variableId;
        public int toNodeId;
        public string toPortName;
    }
}
