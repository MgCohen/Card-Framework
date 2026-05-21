using System.Collections.Generic;
using Scaffold.AppFlow;
using UnityEngine;

namespace Framework.Bootstrap
{
    [DefaultExecutionOrder(-1000)]
    public sealed class ServerAppFlowRoot : AppFlowRoot
    {
        protected override IEnumerable<IScopeLayer> GetInitialLayers()
        {
            yield return new EngineLayer();
        }
    }
}
