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
            yield return new EngineLayer(_isServer);
            yield return new TagLayer(_isServer);
            yield return new EntityLayer(_isServer);
            yield return new RequestLayer(_isServer);
            yield return new StateLayer(_isServer);
            yield return new CommandLayer(_isServer);
        }
    }
}
