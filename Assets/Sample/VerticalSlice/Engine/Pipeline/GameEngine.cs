#nullable enable
namespace VerticalSlice.Engine.Pipeline
{
    /// <summary>
    /// Composition root. The single thing host code (Unity bootstrap, tests,
    /// future networking layer) constructs and holds. Wires <see cref="World"/>,
    /// <see cref="CommandService"/>, <see cref="EffectService"/>, the
    /// shared <see cref="CardEffectStore"/>, and <see cref="RequestsService"/>
    /// together in the right order.
    ///
    /// Authors writing Effects/Commands/Hooks never see GameEngine — they
    /// receive their narrow scope (<c>IEffectScope</c> / <c>ICommandScope</c>
    /// / <c>IHookScope</c>) from the engine when dispatched.
    /// </summary>
    public sealed class GameEngine
    {
        public World World { get; }
        public CommandService Commands { get; }
        public EffectService Effects { get; }
        public RequestsService Requests { get; }
        public CardEffectStore CardEffects { get; }

        public GameEngine()
        {
            World = new World();
            CardEffects = new CardEffectStore();
            Commands = new CommandService(World);
            Effects = new EffectService(World, Commands, CardEffects);
            Requests = new RequestsService(World, Effects);
            Commands.BindEffects(Effects);      // resolve the bidirectional dependency
            Effects.BindRequests(Requests);     // scope.OpenRequest dispatches here
        }
    }
}
