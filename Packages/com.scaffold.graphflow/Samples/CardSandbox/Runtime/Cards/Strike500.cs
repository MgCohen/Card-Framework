#nullable enable
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Scaffold.GraphFlow;
using UnityEngine;

namespace Scaffold.GraphFlow.CardSandbox.Cards
{
    public static class Strike500
    {
        public const int BaseDamage = 5;

        public static CardEffectGraphAsset BuildAsset()
        {
            var entry = new OnPlayRuntime { nodeId = 1, editorGuid = "strike500-entry" };
            var dispatcher = new Strike500Dispatcher { nodeId = 2, editorGuid = "strike500-dispatch" };

            var asset = ScriptableObject.CreateInstance<CardEffectGraphAsset>();
            asset.nodes = new List<RuntimeNode> { entry, dispatcher };
            asset.flowEdges.Add(new Edge
            {
                fromNodeId = 1, fromPortName = "FlowOut",
                toNodeId = 2, toPortName = "FlowIn",
            });
            return asset;
        }
    }

    public sealed class Strike500Dispatcher : RuntimeNode<CardEffectRunner>
    {
        public FlowInPort FlowIn;

        public Strike500Dispatcher()
        {
            FlowIn = FlowInPort.Async(this, nameof(FlowIn), async (Flow flow) =>
            {
                var cmd = new DealDamageCommand { Amount = Strike500.BaseDamage };
                await cmd.Execute(Runner(flow), flow);
                return (FlowOutPort?)null;
            });
            Ports.Add(FlowIn.Name, FlowIn);
        }
    }
}
