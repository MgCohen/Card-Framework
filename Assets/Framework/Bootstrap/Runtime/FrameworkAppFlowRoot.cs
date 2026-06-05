using System.Collections.Generic;
using Scaffold.AppFlow;
using UnityEngine;

namespace Framework.Bootstrap
{
    [DefaultExecutionOrder(-1000)]
    public sealed class FrameworkAppFlowRoot : AppFlowRoot
    {
        [SerializeField] bool _isServer;

        protected override IEnumerable<IScopeLayer> GetInitialLayers()
        {
            yield return new GameplayLayer(_isServer);
        }
    }
}
