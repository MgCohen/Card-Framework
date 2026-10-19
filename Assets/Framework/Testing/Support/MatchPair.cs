using System;
using Framework.Bootstrap;
using Framework.Commands;
using Framework.Engine;
using Framework.Wire;
using NUnit.Framework;
using VContainer;
using VContainer.Unity;

namespace Framework.Testing
{
    public sealed class MatchPair : IDisposable
    {
        LifetimeScope _server;
        LifetimeScope _client;

        public GameEngine      Engine { get; private set; }
        public IObjectResolver Server => _server.Container;
        public IObjectResolver Client => _client.Container;

        MatchPair() { }

        // bindScenario runs once per side; the bool is isServer. Apply handlers must
        // register on both sides, commands only on the server.
        public static MatchPair Start(Action<IContainerBuilder, bool> bindScenario)
        {
            LoopbackTransport.Pair(out var serverTransport, out var clientTransport);
            var pair = new MatchPair();
            pair._server = Build(true,  serverTransport, bindScenario, "AcceptanceServerScope");
            pair._client = Build(false, clientTransport, bindScenario, "AcceptanceClientScope");
            pair.Engine  = pair.Server.Resolve<GameEngine>();
            return pair;
        }

        static LifetimeScope Build(bool isServer, ITransport transport,
                                   Action<IContainerBuilder, bool> bindScenario, string name)
        {
            var scope = LifetimeScope.Create(builder =>
            {
                new GameplayLayer(isServer, transport).Install(builder);
                bindScenario(builder, isServer);
            }, name);
            if (scope.Container == null) scope.Build();
            return scope;
        }

        public CommandResult Execute(Command command) => Engine.Commands.Execute(command);

        // Same events in the same order on both sides means the same apply handlers
        // built the same stores.
        public void AssertStreamsMatch()
        {
            var server = Server.Resolve<EventStream>();
            var client = Client.Resolve<EventStream>();
            Assert.AreEqual(server.Count, client.Count, "event count differs between server and client");
            for (var i = 0; i < server.Count; i++)
                Assert.AreEqual(server[i].GetType(), client[i].GetType(), $"event #{i} type differs");
        }

        public void Dispose()
        {
            if (_client != null) UnityEngine.Object.DestroyImmediate(_client.gameObject);
            if (_server != null) UnityEngine.Object.DestroyImmediate(_server.gameObject);
            _client = null;
            _server = null;
        }
    }
}
