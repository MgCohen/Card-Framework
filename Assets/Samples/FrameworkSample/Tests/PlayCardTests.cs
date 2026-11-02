using Framework.Blocks;
using Framework.Pipeline;
using Framework.RecordIds;
using Framework.TypeKeys;
using Framework.Wire;
using NUnit.Framework;
using VContainer;

namespace Framework.Samples.FrameworkSample.Tests
{
    public class PlayCardTests
    {
        IObjectResolver _server;
        IObjectResolver _client;
        EventStream _serverStream;
        EventStream _clientStream;

        [SetUp]
        public void SetUp()
        {
            // Inside a Framework.* namespace, "Wire" names the namespace, so write the full name.
            var wire = Framework.Wire.Wire.Connect();
            foreach (var network in new[] { wire.ServerNetwork, wire.ClientNetwork })
            {
                network.Register<GroupStart, GroupStart.Packed>();
                network.Register<GroupEnd, GroupEnd.Packed>();
                network.Register<CardMoved, CardMoved.Packed>();
            }
            _serverStream = wire.ServerStream;
            _clientStream = wire.ClientStream;

            var server = new ContainerBuilder();
            server.RegisterInstance(_serverStream).AsSelf().As<IEventEmitter>();
            server.Register<CardStore>(Lifetime.Singleton);
            server.AddBlocks();
            server.AddIdSpace<CardId>(Role.Server);
            server.AddTypeKeys(o => o.Validate = true);
            server.AddKeySpace<Zone, ZoneRegistry>();
            server.AddPipeline<GamePipeline, GameMessage, GameScope, GameView>();
            _server = server.Build();

            var client = new ContainerBuilder();
            client.RegisterInstance(_clientStream).AsSelf().As<IEventEmitter>();
            client.AddBlocks(o => o.Role = Role.Client);
            client.AddKeySpace<Zone, ZoneRegistry>();
            _client = client.Build();
        }

        CardId PutCard(Zone zone)
        {
            var id = _server.Resolve<IdAllocator<CardId>>().Next();
            _server.Resolve<CardStore>().Put(new Card(id, zone));
            return id;
        }

        Result Play(CardId id)
        {
            var pipeline = _server.Resolve<GamePipeline>();
            var e = pipeline.Add(new PlayCard(id));
            pipeline.Run().Wait();
            return Result.From(e);
        }

        [Test]
        public void PlayCard_FromHand_MovesToBoard_AndClientSeesTheSameBlocks()
        {
            var id = PutCard(new Zone.Hand());

            var result = Play(id);

            Assert.That(result.Accepted, Is.True);
            Assert.That(_server.Resolve<CardStore>().ById(id).Zone, Is.EqualTo(new Zone.Board()));

            Assert.That(_clientStream.Count, Is.EqualTo(3));
            Assert.That(_serverStream.Count, Is.EqualTo(3));
            Assert.That(_clientStream[0], Is.TypeOf<GroupStart>());
            Assert.That(_clientStream[1], Is.TypeOf<CardMoved>());
            Assert.That(_clientStream[2], Is.TypeOf<GroupEnd>());
            for (var i = 0; i < 3; i++)
            {
                var onServer = (Block)_serverStream[i];
                var onClient = (Block)_clientStream[i];
                Assert.That(onClient.Position, Is.EqualTo(i + 1));
                Assert.That(onClient.Position, Is.EqualTo(onServer.Position));
                Assert.That(onClient.Id, Is.EqualTo(onServer.Id));
            }

            var start = (GroupStart)_clientStream[0];
            var end = (GroupEnd)_clientStream[2];
            Assert.That(end.StartId, Is.EqualTo(start.Id));
            Assert.That((GroupOutcome)end.Outcome, Is.EqualTo(GroupOutcome.Completed));

            var moved = (CardMoved)_clientStream[1];
            var zones = _client.Resolve<ZoneRegistry>();
            Assert.That(moved.Card, Is.EqualTo(id));
            Assert.That(zones.BySlot(moved.From), Is.TypeOf<Zone.Hand>());
            Assert.That(zones.BySlot(moved.To), Is.TypeOf<Zone.Board>());
        }

        [Test]
        public void PlayCard_NotInHand_IsRejected_AndEmitsNothing()
        {
            var id = PutCard(new Zone.Board());

            var result = Play(id);

            Assert.That(result.State, Is.EqualTo(EnvelopeState.Rejected));
            Assert.That(result.Reason, Does.Contain("not in Hand"));
            Assert.That(_server.Resolve<CardStore>().ById(id).Zone, Is.EqualTo(new Zone.Board()));
            Assert.That(_serverStream.Count, Is.EqualTo(0));
            Assert.That(_clientStream.Count, Is.EqualTo(0));
        }

        [Test]
        public void Composition_HasNoIssues()
        {
            Assert.That(TypeKeyValidator.Validate(typeof(Zone)), Is.Empty);
        }
    }
}
