#nullable enable
using System.Collections.Generic;
using Scaffold.GraphFlow;
using UnityEngine;

namespace Scaffold.GraphFlow.CardSandbox.Cards
{
    public static class PlusOneDamage
    {
        public static CardEffectGraphAsset BuildAsset()
        {
            var entry = new OnTrigger<DamageDealt>
            {
                nodeId = 1, editorGuid = "plusone-trigger", Timing = Timing.Before,
            };
            var mutator = new PlusOneDamageMutator
            {
                nodeId = 2, editorGuid = "plusone-mutator",
            };

            var asset = ScriptableObject.CreateInstance<CardEffectGraphAsset>();
            asset.nodes = new List<RuntimeNode> { entry, mutator };
            asset.flowEdges.Add(new Edge
            {
                fromNodeId = 1, fromPortName = "FlowOut",
                toNodeId = 2, toPortName = "FlowIn",
            });
            asset.connections.Add(new Edge
            {
                fromNodeId = 1, fromPortName = "Event",
                toNodeId = 2, toPortName = "Event",
            });
            return asset;
        }
    }

    public sealed class PlusOneDamageMutator : RuntimeNode<CardEffectRunner>
    {
        public FlowInPort FlowIn;
        public InputPort<DamageDealt> Event;

        public PlusOneDamageMutator()
        {
            Event = new InputPort<DamageDealt>();
            FlowIn = FlowInPort.Sync(this, nameof(FlowIn), flow =>
            {
                var evt = Event.Read(flow);
                if (evt != null) evt.Amount += 1;
                return null;
            });
            Ports.Add(FlowIn.Name, FlowIn);
            Ports.Add(nameof(Event), Event);
        }
    }
}
