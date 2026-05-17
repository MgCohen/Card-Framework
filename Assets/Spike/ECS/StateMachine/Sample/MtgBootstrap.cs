#nullable enable
using StateMachine.Engine;

namespace StateMachine.Sample
{
    /// <summary>
    /// Wires the whole thing up. Tests and the IMGUI scene both go through
    /// this — there's no other path. Construction order matters: engine
    /// before state service (service holds an engine reference), then
    /// PushStateHandler can be registered (it needs both).
    /// </summary>
    public static class MtgBootstrap
    {
        public static (GameEngine engine, StateService state) Build()
        {
            var world    = new World();
            var handlers = new HandlerRegistry();
            var states   = BuildStateRegistry();
            var flow     = new SimplifiedMtgFlow();

            var engine = new GameEngine(world, handlers, states, flow);

            var stateService = new StateService(engine, new MatchNotStartedState());
            engine.State = stateService;

            handlers.Register<PushStateCommand, PushStateResult>(new PushStateHandler(stateService, states));
            handlers.Register<PassCommand,              StandardResult>(new PassHandler());
            handlers.Register<ToggleAttackerCommand,    StandardResult>(new ToggleAttackerHandler());
            handlers.Register<CommitAttackersCommand,   StandardResult>(new CommitAttackersHandler());
            handlers.Register<CommitBlockersCommand,    StandardResult>(new CommitBlockersHandler());
            handlers.Register<DealDamageCommand,        StandardResult>(new DealDamageHandler());
            handlers.Register<ClearTagCommand,          StandardResult>(new ClearTagHandler());
            handlers.Register<IncrementTurnCommand,     StandardResult>(new IncrementTurnHandler());
            handlers.Register<SwapActivePlayerCommand,  StandardResult>(new SwapActivePlayerHandler());
            handlers.Register<UntapAllCommand,          StandardResult>(new UntapAllHandler());
            handlers.Register<DrawCardCommand,          StandardResult>(new DrawCardHandler());

            return (engine, stateService);
        }

        public static IStateRegistry BuildStateRegistry()
        {
            var r = new StateRegistry();

            // Framework reserves 0–9.
            r.Register<MatchNotStartedState>(0, () => new MatchNotStartedState());
            r.Register<PlayingState>        (1, () => new PlayingState());
            r.Register<MatchEndedState>     (2, () => new MatchEndedState());

            // Game-authored kinds start at 10 — spike §v6 convention.
            r.Register<UpkeepState>           (10, () => new UpkeepState());
            r.Register<Main1State>            (11, () => new Main1State());
            r.Register<DeclareAttackersState> (12, () => new DeclareAttackersState());
            r.Register<DeclareBlockersState>  (13, () => new DeclareBlockersState());
            r.Register<CombatDamageState>     (14, () => new CombatDamageState());
            r.Register<Main2State>            (15, () => new Main2State());
            r.Register<EndState>              (16, () => new EndState());

            return r;
        }

        /// <summary>Convenience for tests + sandbox: spawn N creatures with
        /// the given power, all controlled by <paramref name="controller"/>.</summary>
        public static Entity SpawnCreature(World world, PlayerId controller, int power, int toughness)
        {
            var e = world.Create();
            e.Set(MtgTags.Controller, controller);
            e.Set(MtgTags.Power,      power);
            e.Set(MtgTags.Toughness,  toughness);
            return e;
        }
    }
}
