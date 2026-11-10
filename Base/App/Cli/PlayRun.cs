using Cards.Domain.Shared;
using Cards.Slices.CreateCard;
using Cards.Slices.PlayCard;
using Ecs.Contracts.Commands;
using Ecs.Domain;
using Ecs.Domain.Shared;
using Ecs.Handlers.Commands;
using Microsoft.Extensions.DependencyInjection;
using Resources.Domain.Shared;
using Resources.Slices.ChangeResource;
using Trace.Handlers.Commands;
using VSA.Contracts.Commands;
using Wire.Handlers.Commands;

namespace App.Cli;

public static class PlayRun
{
    public static async Task<IReadOnlyList<string>> RunAsync(Match match)
    {
        ICommandSender commands = match.Commands;
        CancellationToken ct = CancellationToken.None;
        EntityId cheap = (await commands.SendAsync(new CreateCardCommand(Zone.Hand, 3), ct)).Value;
        EntityId costly = (await commands.SendAsync(new CreateCardCommand(Zone.Hand, 9), ct)).Value;
        EntityId player = (await commands.SendAsync(new CreateEntity(default), ct)).Value;
        await commands.SendAsync(new ChangeResourceCommand(player, 10, Resource.Mana), ct);

        List<string> lines = new List<string>(TraceText.Legend) { string.Empty };
        lines.Add(World(match.Reader, cheap, costly, player));
        TraceLog trace = match.Services.GetRequiredService<TraceLog>();
        WireLog log = match.Services.GetRequiredService<WireLog>();
        log.Appended += entry => trace.Wire(log.Entries.Count, entry);
        trace.Start();

        await commands.SendAsync(new PlayCardCommand(cheap, player), ct);
        await commands.SendAsync(new PlayCardCommand(cheap, player), ct);
        await commands.SendAsync(new PlayCardCommand(costly, player), ct);

        lines.AddRange(TraceText.Lines(trace.Events));
        lines.Add(World(match.Reader, cheap, costly, player));
        return lines;
    }

    private static string World(IWorldReader world, EntityId cheap, EntityId costly, EntityId player)
    {
        int mana = world.TryGet(world.ById(player), out Mana pool) ? pool.Value : 0;
        return $"[WORLD]   {Card(world, cheap)} | {Card(world, costly)} | player {player.Value}: mana={mana}";
    }

    private static string Card(IWorldReader world, EntityId id)
    {
        Entity card = world.ById(id);
        Zone zone = world.TryGet(card, out CardZone at) ? at.Value : Zone.None;
        int cost = world.TryGet(card, out Cost price) ? price.Value : 0;
        return $"card {id.Value}: zone={zone} cost={cost}";
    }
}
