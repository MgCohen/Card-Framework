using System;
using Framework.Bootstrap;
using Framework.Commands;
using Framework.Engine;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Acceptance
{
    public sealed class MatchHarness : IDisposable
    {
        LifetimeScope _scope;

        public GameEngine Engine { get; private set; }

        MatchHarness() { }

        public static MatchHarness Start(Action<IContainerBuilder> bindScenario)
        {
            var match = new MatchHarness();
            match._scope = LifetimeScope.Create(builder =>
            {
                new EngineLayer(isServer: true).Install(builder);
                new TagLayer(isServer: true).Install(builder);
                new EntityLayer(isServer: true).Install(builder);
                new RequestLayer(isServer: true).Install(builder);
                new StateLayer(isServer: true).Install(builder);
                new CommandLayer(isServer: true).Install(builder);
                bindScenario(builder);
            }, "AcceptanceMatchScope");
            if (match._scope.Container == null) match._scope.Build();
            match.Engine = match._scope.Container.Resolve<GameEngine>();
            return match;
        }

        public CommandResult Execute(Command command) => Engine.Commands.Execute(command);

        public void Dispose()
        {
            if (_scope == null) return;
            UnityEngine.Object.DestroyImmediate(_scope.gameObject);
            _scope = null;
        }
    }
}
