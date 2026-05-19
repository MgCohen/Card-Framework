#nullable enable
using System.Collections.Generic;
using System.Reflection;
using Scaffold.GraphFlow.Nodes;
using Scaffold.Variables;
using UnityEngine;

namespace Scaffold.GraphFlow.CardSandbox.Showcase
{
    public static class StrikeWithVariables
    {
        public static CardEffectGraphAsset BuildAsset()
        {
            var asset = ScriptableObject.CreateInstance<CardEffectGraphAsset>();

            // --- Variables (blackboard) ---
            asset.variables.Add(Var("hp",     new BlackboardInt    { value = 100 }));
            asset.variables.Add(Var("attack", new BlackboardInt    { value = 10 }));
            asset.variables.Add(Var("name",   new BlackboardString { value = "Hero" }));

            // --- Nodes ---
            //
            // OnPlay (1) ─flow─► GetAttack (2) ─data─► DealDamage (3)
            //                                              │
            //                                         flow(Done)
            //                                              │
            //                                              ▼
            //                                          Logger (4)
            //                     ┌──────────────────────────────────┐
            // ObserveHP (5) ─flow─► ObserveLogger (6)               │
            //                └─data(NewValue)─► ObserveLogger.Value │
            //                     └──────────────────────────────────┘

            var entry      = new OnPlayRuntime   { nodeId = 1, editorGuid = "showcase-entry" };
            var getAttack  = new GetVariable<int>  { nodeId = 2, editorGuid = "showcase-getAtk" };
            var dealDamage = new DealDamageNode  { nodeId = 3, editorGuid = "showcase-deal" };
            var logger     = new LogNode         { nodeId = 4, editorGuid = "showcase-log" };
            var observeHp  = new ObserveVariable<int> { nodeId = 5, editorGuid = "showcase-obsHp" };
            var obsLogger  = new LogNode         { nodeId = 6, editorGuid = "showcase-obsLog" };

            SetVariableId(getAttack, "attack");
            SetVariableId(observeHp, "hp");

            asset.nodes = new List<RuntimeNode> { entry, getAttack, dealDamage, logger, observeHp, obsLogger };

            // Flow edges
            asset.flowEdges.Add(new Edge { fromNodeId = 1, fromPortName = nameof(OnPlayRuntime.FlowOut),   toNodeId = 3, toPortName = nameof(DealDamageNode.FlowIn) });
            asset.flowEdges.Add(new Edge { fromNodeId = 3, fromPortName = nameof(DealDamageNode.Done),     toNodeId = 4, toPortName = nameof(LogNode.FlowIn) });
            asset.flowEdges.Add(new Edge { fromNodeId = 5, fromPortName = nameof(ObserveVariable<int>.FlowOut), toNodeId = 6, toPortName = nameof(LogNode.FlowIn) });

            // Data edges
            asset.connections.Add(new Edge { fromNodeId = 2, fromPortName = nameof(GetVariable<int>.Value),       toNodeId = 3, toPortName = nameof(DealDamageNode.Amount) });
            asset.connections.Add(new Edge { fromNodeId = 5, fromPortName = nameof(ObserveVariable<int>.NewValue), toNodeId = 6, toPortName = nameof(LogNode.Value) });

            // Variable-bound input: DealDamage.Label reads the "name" variable directly (no Get node)
            asset.variableEdges.Add(new VariableEdge { variableId = "name", toNodeId = 3, toPortName = nameof(DealDamageNode.Label) });

            return asset;
        }

        static RuntimeVariable Var(string id, VariableDefault def) => new()
        {
            id = id,
            name = id,
            typeName = def.ValueType.AssemblyQualifiedName!,
            defaultValue = def,
        };

        static void SetVariableId(RuntimeNode node, string id)
        {
            var field = node.GetType().GetField("variableId",
                BindingFlags.NonPublic | BindingFlags.Instance);
            field!.SetValue(node, id);
        }
    }

    [System.Serializable]
    public sealed class DealDamageNode : RuntimeNode<CardEffectRunner>
    {
        public FlowInPort FlowIn = null!;
        public FlowOutPort Done = null!;
        public InputPort<int> Amount = null!;
        public InputPort<string> Label = null!;

        public DealDamageNode()
        {
            Amount = new InputPort<int>();
            Label  = new InputPort<string>();
            Done   = new FlowOutPort(this, nameof(Done));
            FlowIn = FlowInPort.Sync(this, nameof(FlowIn), flow =>
            {
                var dmg = Amount.Read(flow);
                var who = Label.Read(flow);
                Runner(flow).Damage.Apply(null, dmg);
                Debug.Log($"[GraphFlow] {who} deals {dmg} damage!");
                return Done;
            });
            Ports.Add(FlowIn.Name, FlowIn);
            Ports.Add(Done.Name, Done);
            Ports.Add(nameof(Amount), Amount);
            Ports.Add(nameof(Label), Label);
        }
    }

    [System.Serializable]
    public sealed class LogNode : RuntimeNode
    {
        public FlowInPort FlowIn = null!;
        public InputPort<int> Value = null!;

        public LogNode()
        {
            Value  = new InputPort<int>();
            FlowIn = FlowInPort.Sync(this, nameof(FlowIn), flow =>
            {
                Debug.Log($"[GraphFlow] Value = {Value.Read(flow)}");
                return null;
            });
            Ports.Add(FlowIn.Name, FlowIn);
            Ports.Add(nameof(Value), Value);
        }
    }
}
