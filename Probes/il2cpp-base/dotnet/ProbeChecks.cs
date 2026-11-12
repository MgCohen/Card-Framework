using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Cards.Domain.Shared;
using Cards.Slices.CreateCard;
using Cards.Slices.MoveCard;
using Ecs.Contracts.Commands;
using Ecs.Infrastructure;
using Friflo.Engine.ECS;
using MemoryPack;
using Microsoft.Extensions.DependencyInjection;
using Network.Domain;
using Peers.Handlers.Commands;
using Resources.Domain.Shared;
using Resources.Slices.ChangeResource;
using VSA.Composition;
using VSA.Contracts;
using VSA.Contracts.Commands;
using Wire.Handlers.Commands;
using EcsComponent = Ecs.Domain.Shared.IComponent;
using EcsEntityId = Ecs.Domain.Shared.EntityId;

namespace BaseProbe
{
    public struct Heat : EcsComponent
    {
        public int Value { get; init; }
    }

    [MemoryPackable]
    public partial struct Label : EcsComponent
    {
        public string Value { get; init; }
    }

    public struct Position : Friflo.Engine.ECS.IComponent
    {
        public int X;
    }

    public struct Flag : ITag
    {
    }

    public static class ProbeChecks
    {
        public static async Task<List<string>> RunAsync()
        {
            List<string> lines = new List<string>();
            Check(lines, "friflo.world", FrifloWorld);
            Check(lines, "vsa.compose", Compose);
            Check(lines, "memorypack.struct-key", () => RoundTrip(new ChangeComponent<Heat>(new EcsEntityId(3), new Heat { Value = 7 })));
            Check(lines, "memorypack.reference-key", () => RoundTrip(new ChangeComponent<Label>(new EcsEntityId(3), new Label { Value = "hi" })));
            await CheckAsync(lines, "vsa.closed-handlers", ClosedHandlersAsync);
            await CheckAsync(lines, "done-check.server-client", DoneCheckAsync);
            return lines;
        }

        private static void Check(List<string> lines, string name, Func<string> check)
        {
            try
            {
                lines.Add("PASS " + name + " -> " + check());
            }
            catch (Exception e)
            {
                lines.Add("FAIL " + name + " -> " + e.GetType().Name + ": " + e.Message);
            }
        }

        private static async Task CheckAsync(List<string> lines, string name, Func<Task<string>> check)
        {
            try
            {
                lines.Add("PASS " + name + " -> " + await check());
            }
            catch (Exception e)
            {
                lines.Add("FAIL " + name + " -> " + e.GetType().Name + ": " + e.Message);
            }
        }

        private static string FrifloWorld()
        {
            EntityStore store = new EntityStore();
            Friflo.Engine.ECS.Entity entity = store.CreateEntity(new Position { X = 4 });
            entity.AddTag<Flag>();
            int x = entity.GetComponent<Position>().X;
            bool tagged = entity.Tags.Has<Flag>();
            entity.DeleteEntity();
            Require(x == 4 && tagged && store.Count == 0, "x=" + x + " tag=" + tagged + " count=" + store.Count);
            return "x=4 tag=true count after delete=0";
        }

        private static string Compose()
        {
            using ServiceProvider services = Services(null).BuildServiceProvider();
            WireCatalog catalog = services.GetRequiredService<WireCatalog>();
            using IServiceScope match = services.GetRequiredService<MatchFactory>().Create();
            match.ServiceProvider.GetRequiredService<IWorldReader>();
            return "catalog types=" + catalog.Count + " hash=" + catalog.Hash.ToString("x16");
        }

        private static string RoundTrip(IMessage entry)
        {
            using ServiceProvider services = Services(null).BuildServiceProvider();
            WireCatalog catalog = services.GetRequiredService<WireCatalog>();
            ArrayBufferWriter<byte> frame = new ArrayBufferWriter<byte>();
            catalog.Write(entry, frame);
            IMessage back = catalog.Read(frame.WrittenSpan);
            Require(Equals(back, entry), "read " + back + ", wrote " + entry);
            return back + " (" + frame.WrittenCount + " bytes)";
        }

        private static async Task<string> ClosedHandlersAsync()
        {
            using ServiceProvider services = Services(null).BuildServiceProvider();
            using IServiceScope match = services.GetRequiredService<MatchFactory>().Create();
            ICommandSender bus = match.ServiceProvider.GetRequiredService<ICommandSender>();
            IWorldReader world = match.ServiceProvider.GetRequiredService<IWorldReader>();
            EcsEntityId card = Value(await bus.SendAsync(new CreateCardCommand(Zone.Hand), default));
            int health = Value(await bus.SendAsync(new ChangeResourceCommand<Health>(card, 5), default));
            int mana = Value(await bus.SendAsync(new ChangeResourceCommand<Mana>(card, 2), default));
            Value(await bus.SendAsync(new ChangeComponent<Heat>(card, new Heat { Value = 9 }), default));
            Value(await bus.SendAsync(new ChangeComponent<Label>(card, new Label { Value = "probe" }), default));
            Ecs.Domain.Entity entity = world.ById(card);
            string label = world.Get<Label>(entity).Value;
            int heat = world.Get<Heat>(entity).Value;
            Require(health == 5 && mana == 2 && heat == 9 && label == "probe", "values differ");
            return "Health=5 Mana=2 Heat=9 Label=probe";
        }

        private static async Task<string> DoneCheckAsync()
        {
            InProcessTransport.Pair(out InProcessTransport serverSide, out InProcessTransport clientSide);
            using ServiceProvider clientHost = Services(s =>
            {
                s.AddSingleton<ITransport>(clientSide);
                s.AddScoped<WireClient>();
            }).BuildServiceProvider();
            using IServiceScope client = clientHost.GetRequiredService<MatchFactory>().Create();
            WireClient wireClient = client.ServiceProvider.GetRequiredService<WireClient>();

            using ServiceProvider serverHost = Services(s =>
            {
                s.AddSingleton<ITransport>(serverSide);
                s.AddScoped<WireServer>();
            }).BuildServiceProvider();
            using IServiceScope server = serverHost.GetRequiredService<MatchFactory>().Create();
            server.ServiceProvider.GetRequiredService<WireServer>();

            ICommandSender bus = server.ServiceProvider.GetRequiredService<ICommandSender>();
            EcsEntityId card = Value(await bus.SendAsync(new CreateCardCommand(Zone.Hand), default));
            Value(await bus.SendAsync(new MoveCardCommand(card, Zone.Board), default));
            Value(await bus.SendAsync(new ChangeResourceCommand<Health>(card, 5), default));
            await wireClient.Idle;

            IWorldReader serverWorld = server.ServiceProvider.GetRequiredService<IWorldReader>();
            IWorldReader clientWorld = client.ServiceProvider.GetRequiredService<IWorldReader>();
            Ecs.Domain.Entity c = clientWorld.ById(card);
            IReadOnlyList<IMessage> serverLog = server.ServiceProvider.GetRequiredService<WireLog>().Entries;
            IReadOnlyList<IMessage> clientLog = client.ServiceProvider.GetRequiredService<WireLog>().Entries;
            bool equal = clientWorld.Count == serverWorld.Count
                && clientWorld.HasTag<Card>(c)
                && clientWorld.Get<CardZone>(c).Value == Zone.Board
                && clientWorld.Get<Health>(c).Value == 5
                && serverLog.SequenceEqual(clientLog);
            Require(equal, "worlds or logs differ");
            return "entities=" + clientWorld.Count + " log entries=" + serverLog.Count + " zone=Board health=5";
        }

        private static IServiceCollection Services(Action<IServiceCollection>? extra)
        {
            IServiceCollection services = new ServiceCollection().AddVsa();
            services.AddSingleton(WireTypes.Of(t => t.Component<Heat>().Component<Label>()));
            extra?.Invoke(services);
            return services;
        }

        private static T Value<T>(Result<T> result)
        {
            if (!result.IsSuccess)
            {
                throw new InvalidOperationException("send failed: " + result.Error);
            }

            return result.Value;
        }

        private static void Require(bool condition, string detail)
        {
            if (!condition)
            {
                throw new InvalidOperationException(detail);
            }
        }
    }
}
