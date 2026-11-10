using Cards.Domain.Shared;
using Microsoft.Extensions.DependencyInjection;
using VSA.Module;
using Wire.Handlers.Commands;

namespace Cards;

public sealed class CardsModule : Module
{
    public override void Configure(ModuleBuilder module)
    {
        module.Services.AddSingleton(WireTypes.Of(t => t.Component<CardZone>().Component<Cost>().Tag<Card>()));
    }
}
