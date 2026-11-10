using App.Cli;
using Ecs.Handlers.Commands;
using Microsoft.Extensions.Hosting;
using VSA.Composition;

if (args.Length != 1 || args[0] != "play")
{
    Console.Error.WriteLine("Usage: dotnet run --project App/Cli -- play");
    return 1;
}

using IHost host = VsaBuilder.Create().UseGenericHost().Build();
using Match match = new Match(host.Services);
foreach (string line in await PlayRun.RunAsync(match))
{
    Console.WriteLine(line);
}

return 0;
