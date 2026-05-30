using System.Collections.Generic;
using Scaffold.AppFlow;
using UnityEngine;

namespace Framework.Bootstrap
{
    // Single root for both server and client scopes. `_isServer` flows down into every layer
    // that has a per-side delta: EngineLayer (ServerEngine vs ClientEngine + bridge),
    // TagLayer and EntityLayer (gate the command-service registrations).
    [DefaultExecutionOrder(-1000)]
    public sealed class FrameworkAppFlowRoot : AppFlowRoot
    {
        [SerializeField] bool _isServer;

        protected override IEnumerable<IScopeLayer> GetInitialLayers()
        {
            yield return new EngineLayer(_isServer);
            yield return new TagLayer(_isServer);
            yield return new EntityLayer(_isServer);
        }
    }
}
