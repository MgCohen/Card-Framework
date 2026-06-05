using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Framework.Bootstrap;
using Framework.Engine;
using NUnit.Framework;
using Scaffold.AppFlow;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Framework.Integration.Tests
{
    // Drives the ACTUAL composition root: FrameworkAppFlowRoot through the real AppFlowHost,
    // installing exactly the layer set GetInitialLayers() yields — the same path RunStartupAsync
    // takes in play mode. Guards the regression where each system was its own nested AppFlow
    // layer (root -> Engine -> ... -> Command): GameEngine, registered shallow, could not see
    // the services owned by descendant layers, so the root could not compose a server engine.
    // With a single GameplayLayer installing the systems flat, the top scope resolves the engine.
    public class RealAppFlowRootCompositionTests
    {
        GameObject _go;

        [TearDown]
        public void TearDown()
        {
            if (_go != null) { Object.DestroyImmediate(_go); _go = null; }
        }

        [Test]
        public async Task Real_Root_Composes_A_Server_GameEngine()
        {
            _go = new GameObject("FrameworkAppFlowRoot");
            var root = _go.AddComponent<FrameworkAppFlowRoot>();
            SetIsServer(root, true);
            if (root.Container == null) root.Build(); // base Configure: proxy + error + progress

            var host = new AppFlowHost(root);
            await host.InstallAllAsync(InitialLayers(root), CancellationToken.None);

            var engine = host.Top.Resolve<GameEngine>();

            Assert.IsNotNull(engine);
            Assert.IsNotNull(engine.Entities);
            Assert.IsNotNull(engine.Tags);
            Assert.IsNotNull(engine.Requests);
            Assert.IsNotNull(engine.States);
            Assert.IsNotNull(engine.Commands);
            Assert.IsNotNull(engine.EntityStore);
            Assert.IsNotNull(engine.TagStore);
        }

        // The root reads _isServer from a serialized field and yields its layers from a
        // protected method; reflection lets the test drive the real component without widening
        // production visibility just for the test.
        static void SetIsServer(FrameworkAppFlowRoot root, bool value)
        {
            var f = typeof(FrameworkAppFlowRoot)
                .GetField("_isServer", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(f, "could not find FrameworkAppFlowRoot._isServer via reflection");
            f.SetValue(root, value);
        }

        static List<IScopeLayer> InitialLayers(FrameworkAppFlowRoot root)
        {
            var m = typeof(AppFlowRoot)
                .GetMethod("GetInitialLayers", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(m, "could not find AppFlowRoot.GetInitialLayers via reflection");
            return ((IEnumerable<IScopeLayer>)m.Invoke(root, null)).ToList();
        }
    }
}
