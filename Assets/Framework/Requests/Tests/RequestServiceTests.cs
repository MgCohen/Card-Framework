using System.Collections.Generic;
using Framework.Wire;
using NUnit.Framework;
using WireBridge = Framework.Wire.Wire;

namespace Framework.Requests.Tests
{
    // Mirror round-trip — the load-bearing verification of step 5a.
    //
    // Server opens a typed Request via the service, which emits a Request wire event.
    // The wire forwards to the client; both apply handlers insert into their respective
    // RequestStore. Server then emits a Response wire event (in production: triggered by
    // a client→server wire delivery; today: simulated directly server-side since the wire
    // is server→client one-direction). Both sides drop the request from their store and
    // the server's TCS resolves with the typed Response — the awaiter on Open(...) gets
    // the typed value back.
    public class RequestServiceTests
    {
        sealed class Side
        {
            public EventStream    Stream  { get; }
            public RequestStore   Store   { get; }
            public RequestService Service { get; }   // null on client
            public HandlerRegistry Registry { get; }

            public Side(EventStream stream, bool isServer)
            {
                Stream = stream;
                Store = new RequestStore();
                Service = isServer ? new RequestService(Stream) : null;

                var handlers = new List<IWireEventHandler>
                {
                    new TestPickRequestHandler(Store),
                    new TestPickResponseHandler(Store, Service),
                };
                Registry = new HandlerRegistry(Stream, handlers);
            }
        }

        static (Side server, Side client, WireConnection w) Build()
        {
            var w = WireBridge.Connect();
            w.ServerNetwork.Register<TestPickRequest,  TestPickRequest.Packed>();
            w.ClientNetwork.Register<TestPickRequest,  TestPickRequest.Packed>();
            w.ServerNetwork.Register<TestPickResponse, TestPickResponse.Packed>();
            w.ClientNetwork.Register<TestPickResponse, TestPickResponse.Packed>();

            var server = new Side(w.ServerStream, isServer: true);
            var client = new Side(w.ClientStream, isServer: false);
            return (server, client, w);
        }

        [Test]
        public void Open_Inserts_Request_On_Both_Sides()
        {
            var (server, client, _) = Build();

            var task = server.Service.Open(new TestPickRequest
            {
                ResponderId = 42, IsCancellable = true, OptionCount = 3,
            });

            Assert.IsFalse(task.IsCompleted, "task should be pending until a response arrives");
            Assert.AreEqual(1, server.Store.Open.Count);
            Assert.AreEqual(1, client.Store.Open.Count);

            var serverReq = server.Store.Get<TestPickRequest>(1);
            var clientReq = client.Store.Get<TestPickRequest>(1);
            Assert.IsNotNull(serverReq);
            Assert.IsNotNull(clientReq);
            Assert.AreEqual(42, clientReq.ResponderId);
            Assert.AreEqual(true, clientReq.IsCancellable);
            Assert.AreEqual(3, clientReq.OptionCount);
        }

        [Test]
        public void Response_Drops_Request_On_Both_Sides_And_Completes_Task()
        {
            var (server, client, _) = Build();

            var task = server.Service.Open(new TestPickRequest { OptionCount = 3 });
            Assert.AreEqual(1, server.Store.Open.Count);
            Assert.AreEqual(1, client.Store.Open.Count);

            // Server emits response (in production: triggered by client→server wire delivery)
            server.Stream.Emit(new TestPickResponse { RequestId = 1, Picked = 2 });

            Assert.AreEqual(0, server.Store.Open.Count, "server store drops on response");
            Assert.AreEqual(0, client.Store.Open.Count, "client store drops on response");
            Assert.IsTrue(task.IsCompleted, "task should resolve when response arrives");
            Assert.AreEqual(2, task.Result.Picked, "typed response carries Picked back");
            Assert.AreEqual(1, task.Result.RequestId);
        }

        [Test]
        public void Open_Allocates_Monotonic_Ids_Per_Session()
        {
            var (server, client, _) = Build();

            var t1 = server.Service.Open(new TestPickRequest { OptionCount = 1 });
            var t2 = server.Service.Open(new TestPickRequest { OptionCount = 2 });
            var t3 = server.Service.Open(new TestPickRequest { OptionCount = 3 });

            Assert.AreEqual(3, server.Store.Open.Count);
            Assert.AreEqual(3, client.Store.Open.Count);
            Assert.IsNotNull(client.Store.Get<TestPickRequest>(1));
            Assert.IsNotNull(client.Store.Get<TestPickRequest>(2));
            Assert.IsNotNull(client.Store.Get<TestPickRequest>(3));

            // Resolve out of order — each task picks up the right typed response by RequestId.
            server.Stream.Emit(new TestPickResponse { RequestId = 2, Picked = 22 });
            Assert.IsFalse(t1.IsCompleted);
            Assert.IsTrue (t2.IsCompleted);
            Assert.IsFalse(t3.IsCompleted);
            Assert.AreEqual(22, t2.Result.Picked);

            server.Stream.Emit(new TestPickResponse { RequestId = 1, Picked = 11 });
            server.Stream.Emit(new TestPickResponse { RequestId = 3, Picked = 33 });
            Assert.AreEqual(11, t1.Result.Picked);
            Assert.AreEqual(33, t3.Result.Picked);
            Assert.AreEqual(0, server.Store.Open.Count);
            Assert.AreEqual(0, client.Store.Open.Count);
        }

        [Test]
        public void TResponse_Is_Typed_At_The_Awaiter()
        {
            // Lock the type-inference shape: Open<TResponse>(Request<TResponse>) returns
            // Task<TResponse> — the awaiter gets the concrete TestPickResponse type back,
            // no cast or dictionary lookup. This is the whole point of Fork A's A2 pick.
            var (server, _, _) = Build();

            var task = server.Service.Open(new TestPickRequest());

            // Compile-time check: task.Result is TestPickResponse, not Response.
            System.Threading.Tasks.Task<TestPickResponse> typed = task;
            Assert.IsNotNull(typed);
        }

        [Test]
        public void Cancel_All_Pending_Cancels_The_Awaiter_Tasks()
        {
            var (server, _, _) = Build();

            var t1 = server.Service.Open(new TestPickRequest());
            var t2 = server.Service.Open(new TestPickRequest());

            server.Service.CancelAllPending();

            Assert.IsTrue(t1.IsCanceled);
            Assert.IsTrue(t2.IsCanceled);
        }

        [Test]
        public void Unmatched_Response_Is_A_NoOp()
        {
            // Defensive: a Response whose RequestId isn't in _pending shouldn't throw.
            // Could happen if the request was already cancelled / cleaned up.
            var (server, _, _) = Build();

            Assert.DoesNotThrow(() =>
                server.Stream.Emit(new TestPickResponse { RequestId = 999, Picked = 0 }));
        }
    }
}
