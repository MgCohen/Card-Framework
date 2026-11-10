using App.Cli;
using Ecs.Handlers.Commands;
using Microsoft.Extensions.Hosting;
using Trace.Handlers.Commands;
using VSA.Composition;

namespace Trace.Tests;

// The run of "dotnet run --project App/Cli -- play": the legend, the World before, three plays, the World after.
public sealed class PlayRunTests
{
    [Test]
    public async Task Play_PrintsTheLegend_TheTrace_AndTheWorld()
    {
        using IHost host = VsaBuilder.Create().UseGenericHost().Build();
        using Match match = new Match(host.Services);

        IReadOnlyList<string> lines = await PlayRun.RunAsync(match);
        foreach (string line in lines)
        {
            TestContext.Out.WriteLine(line);
        }

        Assert.That(lines[0], Is.EqualTo("LEGEND"));
        string[] world = lines.Where(l => l.StartsWith("[WORLD]", StringComparison.Ordinal)).ToArray();
        Assert.That(world, Has.Length.EqualTo(2));
        Assert.That(world[0], Does.Contain("zone=Hand cost=3").And.Contain("mana=10"));
        Assert.That(world[1], Does.Contain("zone=Board cost=3").And.Contain("mana=7"));
        Assert.That(lines.Count(l => l.StartsWith("[SEND]    PlayCardCommand", StringComparison.Ordinal)),
            Is.EqualTo(1));
        Assert.That(lines.Count(l => l.StartsWith("[REJECT]  PlayCardCommand", StringComparison.Ordinal)),
            Is.EqualTo(2));
        Assert.That(lines, Has.Some.EndsWith("Error(Cards.NotInHand)"));
        Assert.That(lines, Has.Some.EndsWith("Error(Cards.NotEnoughMana)"));
        IEnumerable<string> trace = lines.Skip(TraceText.Legend.Count);
        Assert.That(trace.Count(l => l.TrimStart().StartsWith("[WIRE]", StringComparison.Ordinal)), Is.EqualTo(2));
    }
}
