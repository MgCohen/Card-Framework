using System.Collections.Generic;
using Framework.Wire;
using NUnit.Framework;
using WireBridge = Framework.Wire.Wire;

namespace Framework.States.Tests
{
    // Mirror round-trip — the load-bearing verification of step 5b.
    //
    // Server pushes a state via the service, which emits a StatePushEvent<TState>. The wire
    // forwards to the client; both sides' StatePushHandler<TState> writes Current. Server and
    // client converge on the same cursor. State is zero-data — the closed type carried by the
    // channel IS the payload, so there's nothing to (de)serialize beyond the empty Packed
    // struct (the AutoPacker zero-field codegen that this feature depends on).
    public class StateServiceTests
    {
        sealed class Side
        {
            public EventStream     Stream   { get; }
            public StateStore      Store    { get; }
            public StateService    Service  { get; }   // null on client
            public HandlerRegistry Registry { get; }

            public Side(EventStream stream, bool isServer)
            {
                Stream = stream;
                Store = new StateStore();
                Service = isServer ? new StateService(Stream) : null;

                var handlers = new List<IWireEventHandler>
                {
                    new StatePushHandler<MatchNotStartedState>(Store),
                    new StatePushHandler<PlayingState>(Store),
                    new StatePushHandler<MatchEndedState>(Store),
                };
                Registry = new HandlerRegistry(Stream, handlers);
            }
        }

        static (Side server, Side client, WireConnection w) Build()
        {
            var w = WireBridge.Connect();
            RegisterBothSides<MatchNotStartedState>(w);
            RegisterBothSides<PlayingState>(w);
            RegisterBothSides<MatchEndedState>(w);

            var server = new Side(w.ServerStream, isServer: true);
            var client = new Side(w.ClientStream, isServer: false);
            return (server, client, w);
        }

        static void RegisterBothSides<TState>(WireConnection w) where TState : GameState, new()
        {
            w.ServerNetwork.Register<StatePushEvent<TState>, StatePushEvent<TState>.Packed>();
            w.ClientNetwork.Register<StatePushEvent<TState>, StatePushEvent<TState>.Packed>();
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
            // Re-entering a state is a legitimate self-loop (5c OnEnter/OnExit would re-fire).
            // The 5b cursor stays converged across a repeat push.
            var (server, client, _) = Build();

            server.Service.Push<PlayingState>();
            server.Service.Push<PlayingState>();

            Assert.IsInstanceOf<PlayingState>(server.Store.Current);
            Assert.IsInstanceOf<PlayingState>(client.Store.Current);
        }

        [Test]
        public void Each_State_Type_Is_A_Distinct_Channel()
        {
            // Discrimination is purely by closed type — distinct empty Packed structs, distinct
            // channels. Also proves the zero-field Packed codegen actually emitted a nested type.
            Assert.AreNotEqual(typeof(StatePushEvent<PlayingState>.Packed),
                               typeof(StatePushEvent<MatchEndedState>.Packed));
        }
    }
}
