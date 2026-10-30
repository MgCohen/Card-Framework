using System.Collections.Generic;
using Framework.Wire;
using NUnit.Framework;
using WireBridge = Framework.Wire.Wire;

namespace Framework.States.Tests
{
    // Mirror round-trip — the load-bearing verification of the State feature.
    //
    // Server pushes a state via the service, which emits a single slot-discriminated
    // StatePushEvent. The wire forwards to the client; both sides' one StatePushHandler resolves
    // the cursor from the slot via the GameStateRegistry and writes Current. Server and client
    // converge on the same cursor.
    public class StateServiceTests
    {
        sealed class Side
        {
            public EventStream     Stream   { get; }
            public StateStore      Store    { get; }
            public StateService    Service  { get; }   // null on client
            public HandlerRegistry Registry { get; }

            public Side(EventStream stream, bool isServer, GameStateRegistry states)
            {
                Stream = stream;
                Store = new StateStore();
                Service = isServer ? new StateService(Stream) : null;

                var handlers = new List<IWireEventHandler>
                {
                    new StatePushHandler(Store, states),
                };
                Registry = new HandlerRegistry(Stream, handlers);
            }
        }

        static (Side server, Side client, WireConnection w) Build()
        {
            var w = WireBridge.Connect();
            // One channel for every state — slot-discriminated, both sides.
            w.ServerNetwork.Register<StatePushEvent, StatePushEvent.Packed>();
            w.ClientNetwork.Register<StatePushEvent, StatePushEvent.Packed>();

            var states = GameStateRegistry.WithBaselines();
            var server = new Side(w.ServerStream, isServer: true,  states);
            var client = new Side(w.ClientStream, isServer: false, states);
            return (server, client, w);
        }

        [Test]
        public void Store_Starts_On_MatchNotStarted()
        {
            var (server, client, _) = Build();

            Assert.IsInstanceOf<MatchNotStartedState>(server.Store.Current);
            Assert.IsInstanceOf<MatchNotStartedState>(client.Store.Current);
        }

        [Test]
        public void Push_Mirrors_Current_To_Both_Sides()
        {
            var (server, client, _) = Build();

            server.Service.Push<PlayingState>();

            Assert.IsInstanceOf<PlayingState>(server.Store.Current);
            Assert.IsInstanceOf<PlayingState>(client.Store.Current);
        }

        [Test]
        public void Sequential_Pushes_Advance_Both_Cursors()
        {
            var (server, client, _) = Build();

            server.Service.Push<PlayingState>();
            Assert.IsInstanceOf<PlayingState>(client.Store.Current);

            server.Service.Push<MatchEndedState>();
            Assert.IsInstanceOf<MatchEndedState>(server.Store.Current);
            Assert.IsInstanceOf<MatchEndedState>(client.Store.Current);
        }

        [Test]
        public void Re_Push_Same_State_Stays_Converged()
        {
            // Re-entering a state is a legitimate self-loop. The cursor stays converged.
            var (server, client, _) = Build();

            server.Service.Push<PlayingState>();
            server.Service.Push<PlayingState>();

            Assert.IsInstanceOf<PlayingState>(server.Store.Current);
            Assert.IsInstanceOf<PlayingState>(client.Store.Current);
        }

        [Test]
        public void States_Are_Discriminated_By_Distinct_Slots()
        {
            // Discrimination is now by int slot, not closed type. Distinct kinds carry distinct
            // slots, and the baseline registry round-trips slot -> kind.
            Assert.AreNotEqual(new PlayingState().Slot, new MatchEndedState().Slot);

            var states = GameStateRegistry.WithBaselines();
            Assert.IsInstanceOf<PlayingState>(states.BySlot(new PlayingState().Slot));
            Assert.IsInstanceOf<MatchEndedState>(states.BySlot(new MatchEndedState().Slot));
        }
    }
}
