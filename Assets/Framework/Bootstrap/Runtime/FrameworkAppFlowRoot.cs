using System.Collections.Generic;
using Scaffold.AppFlow;
using UnityEngine;

namespace Framework.Bootstrap
{
    // Single root for both server and client scopes. The `_isServer` field is the only
    // asymmetry — flows down into EngineLayer to pick WireForwarder vs NetworkInboundBridge.
    // EntityLayer and TagLayer are side-agnostic.
    [DefaultExecutionOrder(-1000)]
    public sealed class FrameworkAppFlowRoot : AppFlowRoot
    {
        [SerializeField] bool _isServer;

        protected override IEnumerable<IScopeLayer> GetInitialLayers()
        {
            yield return new EngineLayer(_isServer);
            yield return new TagLayer();
            yield return new EntityLayer();
        }
    }
}
