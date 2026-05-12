---
type: ledger
tags: [#open-items]
---

# Open Items

Single ledger of loose ends — durable design questions, deferred work, and known TODOs that aren't in flight right now. Each entry is self-contained: reading any single one tells the full story (what's known, what's open, why it matters, where to look).

**Resolution = delete the entry.** The resolving doc owns the answer (Model section, ADR, feature note, etc.). Git keeps deletion history if ever needed: `git log --diff-filter=D ledgers/OPEN-ITEMS.md`.

**Optional `kind:`** per entry — `question` (we don't know yet), `task` (we know what to do, deferred), `defer` (decided not now, revisit on trigger).

---

## Engine + runtime

- id: oq-entity-destruction | kind: question | **Entity destruction / Cleanup lifecycle** — what "destroyed" means for the Store, when the framework Cleanup hook fires relative to the destruction command envelope, and what happens to in-flight dispatches referencing the destroyed entity. Load-bearing for ongoing effects that self-destruct.
  References: `Product/features/entities/models/02-instance.md` §Lifecycle; `Product/features/entities/models/05-attachment.md` §Cleanup is automatic.

- id: oq-attachment-storage | kind: question | **Attachment storage location** — whether attachments (equipment, auras, buffs) live in a sub-zone on the host, a global attachment registry, or a dedicated zone. Load-bearing for the ongoing-effects attachment model.
  References: `Product/features/entities/models/05-attachment.md` §Creating and attaching.

<!-- oq-effect-pipeline — closed 2026-05-11 by ADR-0011 cascade.
     The design seam is now canonical: ADR-0011 + the new Models
     features/effects/models/01-effect.md (async-coroutine IEffect.Run),
     features/effects/models/02-effect-scope.md (single async Dispatch surface),
     features/effects/models/05-pending-effects.md (universal queue + drain policy),
     features/effects/models/06-request-system.md (state-linked request system).
     The 2026-05-08 effect-pipeline spike is archived at
     Product/spikes/archive/effect-pipeline.md. No remaining design surface here. -->


- id: oq-effect-authoring | kind: question | **Effect authoring vocabulary** — `IEffect.Run(scope) : Task` is the locked runtime contract (per [ADR-0011](Product/decisions/0011-effect-orchestration-dispatch-requests.md) D16 and [`features/effects/models/01-effect.md`](Product/features/effects/models/01-effect.md)). The *authoring vocabulary* — closed-set primitives ("deal damage / draw N / apply modifier") composed into effects, free-form code, graph-shaped, hybrid — is unsettled. How a `CardDefinition` holds the recipe that constructs an `IEffect` from `(Source, EffectId, Payload)` at fire time is open. Tied to the broader `bl-authoring-feature` task. Vertical-slice ships with free-form C# `Run` bodies; vocabulary work surfaces with the authoring feature.
  References: `Product/decisions/0011-effect-orchestration-dispatch-requests.md`, `Product/features/effects/models/01-effect.md`, `Product/features/effects/models/05-pending-effects.md`.

- id: oq-gamerules-composition | kind: question | **`GameRulesRuntime` composition** — a single `GameRulesRuntime<TDefinition>` implementation is shown; in practice it may be composed of smaller rule modules. `IAvailableActions` service surface (`Set` / `Append` / `Clear`) supports module composition mechanically. Modular rules likely double-generic on `(TDefinition, TModuleConfig)` and compose alongside the central runtime. The composition contract (provider/policy ordering, enable/disable, sub-module DI surface) is deliberately undesigned for now.
  References: `Product/features/engine/models/03-game-rules-runtime.md` §Composition.

- id: oq-trigger-ordering | kind: question | **Trigger ordering within fold** — what determines sequence when multiple triggers are registered for the same type: FIFO by registration, priority weight, game-configurable, or something else.

<!-- oq-effect-cancellation-on-match-end — closed 2026-05-11 by the spike Q1
     cancellation lock. Cancellation rides on the typed Response's
     `bool Cancelled` flag (set on Response base); match-end dispatches a
     `Cancelled = true` typed response for every open request. No CancellationToken
     threading needed at the IEffectScope.Dispatch level — the response-flag pattern
     covers both player-initiated cancel and match-end cancel uniformly. See
     features/effects/models/06-request-system.md (banner) +
     decisions/0011-effect-orchestration-dispatch-requests.md (refresh). -->

- id: oq-trigger-subscription-entry | kind: question | **Trigger subscription entry point for regular cards** — `effects/03 §6` shows a handler calling `AbilityDefinition.Register` for attachments. For regular cards entering play (zone entry), no system or lifecycle hook is shown that calls `Register`. Tied to game lifecycle and zone-entry events.
  References: `Product/features/triggers/models/02-trigger-pipeline.md` §Priority and ordering.

- id: oq-view-model-host-shape | kind: question | **Framework `ViewModel` base class shape** — `MatchStateViewModel<TState>` extends a `ViewModel` base whose shape (animation hooks, observable wiring, disposal, lifecycle) is host-MVVM-toolkit-dependent. Pin when the host toolkit is chosen (Unity scene VM library, .NET MAUI, web MVVM lib). Today this Model only pins the `Bind` + `Map` contract.
  References: `Product/decisions/0012-presentation-per-state-view-models.md`, `Product/features/presentation/models/01-match-state-view-model.md`.

- id: oq-history-envelope | kind: question | **`HistoryEnvelope` typed definition** — currently pseudocode with named fields (`envelopeId`, `originCommandId`, `actor`, `entries[]`); no typed C# record exists. `EnvelopeCause` is glossary-named but undefined. Load-bearing for replay, multiplayer transport, and the presentation history loop.
  References: `Product/features/actions/models/01-command-pipeline.md` §History Envelope.

- id: oq-targeting-pre-play | kind: defer | **Pre-play targeting** — a card's play-condition that requires a chosen target *before* the play-card command fires (target is a parameter of the play, not a request inside the resolution). Likely folds into a richer `PlayCardCommand(card, target)` whose cast-validator accepts the target. Differs from mid-effect target requests (which live in the request system per [ADR-0011](Product/decisions/0011-effect-orchestration-dispatch-requests.md) D17 and [`features/effects/models/06-request-system.md`](Product/features/effects/models/06-request-system.md)).
  Trigger: first vertical-slice game with pre-play targeting requirements (Magic-style auras, choose-cast-mode cards).
  References: `Product/decisions/0011-effect-orchestration-dispatch-requests.md` § Defers.

- id: oq-validator-defaults | kind: question | **Framework-default validator set — registration-order crispness and additional defaults.** [ADR-0010](Product/decisions/0010-validator-handler-split.md) lands two framework defaults: `SessionGate` (rejects when `engine.Session` is not accepting commands) and `MatchStateActionGate` (rejects commands whose type isn't in `MatchStateSlice.Current.Actions`). Both register first by convention; games append. Still open: (a) whether the framework enforces ordering (e.g. via a `[ValidatorOrder]` attribute or `services.AddValidatorBefore<...>` extension) or leaves it as documented convention, and (b) whether a third framework-default validator joins them (candidates: a disposal guard, a re-entrancy guard).
  References: `Product/decisions/0010-validator-handler-split.md`, `Product/features/actions/models/06-command-validator.md`.

- id: oq-effect-save-load | kind: defer | **Mid-effect save/load** — effects are async coroutines (per [ADR-0011](Product/decisions/0011-effect-orchestration-dispatch-requests.md) D16 and [`features/effects/models/01-effect.md`](Product/features/effects/models/01-effect.md)); a C# `Task` continuation isn't serializable. Save/load can happen at quiescent points (no effect's `Run` currently in flight). If a game later forces mid-effect save/load, would force state-machine effects — a separate spike.
  Trigger: first save/load consumer that needs mid-effect serialization.
  References: `Product/decisions/0011-effect-orchestration-dispatch-requests.md`, `Product/features/effects/models/01-effect.md`.

- id: oq-drain-policy-composition | kind: question | **Drain-policy composition Model** — the engine's drain decision composes multiple state-linked services: `MatchState.AllowsDrain` (game-state hook), `RequestsSlice` empty, `PendingEffectsSlice` non-empty, plus game overrides. Documented in [`features/effects/models/05-pending-effects.md`](Product/features/effects/models/05-pending-effects.md) §Drain policy and in [`features/engine/models/03-game-rules-runtime.md`](Product/features/engine/models/03-game-rules-runtime.md) (default `Evaluate` body). Open: whether the composition rule grows into its own Model (`engine/10-drain-policy.md`) or stays folded across the two existing homes. Decide when game-side overrides start composing in non-trivial ways.
  References: `Product/decisions/0011-effect-orchestration-dispatch-requests.md`, `Product/features/effects/models/05-pending-effects.md`, `Product/features/engine/models/03-game-rules-runtime.md`.

- id: bl-actor-ownership-reverse-index | kind: defer | **Reverse-direction lookup on `OwnershipState`** — `OwnershipState` is one-way today (`Reference → IActor`). Reverse queries ("what does this player own?") are O(N) filters or game-side aggregates. When a consumer surfaces (e.g., a presentation view that renders all of a player's cards, a rule that needs O(1) reverse lookup at scale), extend `OwnershipState` with a parallel reverse index (`IActor → ImmutableSet<Reference>`) — same two-way pattern as `CardMapState`. Surfaced by Q5 of the domain modeling design pass (2026-05-06).
  Trigger: first reverse-lookup consumer surfaces.
  References: `Product/features/engine/models/06-game-definition.md` § Ownership and zone assignment; `Product/decisions/0005-objects-over-ids.md`.

- id: bl-zone-visibility-custom-policy-serialization | kind: defer | **`ZoneVisibilityPolicy.Custom` serialization** — the named `ZoneVisibilityPolicy` cases (`AllPublic`, `AllHidden`, `OwnerSees`, `OwnerSeesOthersCount`, `OwnerCountOthersNothing`) round-trip cleanly through replay/save/network. The `Custom(Func<IActor, IActor?, VisibilityResult>)` escape hatch carries an arbitrary delegate that breaks serialization. Same problem class as request predicates (resolved by [ADR-0011](Product/decisions/0011-effect-orchestration-dispatch-requests.md) and [`features/effects/models/06-request-system.md`](Product/features/effects/models/06-request-system.md) by carrying IDs/data instead of `Func` fields). Story: either (a) replace `Custom` with a registered-by-id resolver (game registers an `IZoneVisibilityResolver` keyed by id; `Custom(string ResolverId)` records the id), (b) drop `Custom` and force games to add a slice-based override mechanism, or (c) accept that `Custom` games can't replay/network. Surfaced by Q7 of the open-seams design pass (2026-05-06).
  Trigger: first game uses `Custom`, OR networking/replay design forces serialization rules.
  References: `Product/features/zones/models/01-zone-model.md` § Zone visibility policy; `Product/decisions/0011-effect-orchestration-dispatch-requests.md`; `Product/features/effects/models/06-request-system.md`.

- id: bl-card-resolution-helper-shape | kind: defer | **CardRef → Card resolution helper location** — the two-hop resolution (`CardRef → CardMapState → Ref<Card> → Catalog → Card`) needs a single helper to avoid call-site verbosity. Candidate shapes: extension method on `Store` (`store.ResolveCard(cardRef)`), instance method on `CardRef` (`cardRef.Resolve(store)`), shared utility class. All work; choice is ergonomic. Surfaced by Q6 of the open-seams design pass (2026-05-06); deferred to code time.
  Trigger: first concrete implementation requires picking a shape.
  References: `Product/features/cards/models/01-card-model.md` § Resolution surfaces.

- id: bl-pile-refs-index | kind: question | **Engine-side index slice for spawned per-game zone references** — `PileCounterGameDefinition.PileRefsAt(state)` (and its analog for any game with a fixed set of game-shared zones) needs an engine-side index slice (`PileIndexState` or similar generic shape — `Dict<TGameKey, Ref<Zone>>` or `IReadOnlyList<Ref<Zone>>`) populated during the factory's Build walk so the runtime and intent mapper can enumerate spawned zone refs without scanning `OwnershipState` or filtering by definition type. Open question: framework-provided generic index, game-side helper, or some intermediate. Surfaced by the PileCounter sample refresh (2026-05-06).
  Trigger: first vertical-slice implementation needs concrete enumeration of game-shared zones spawned at Build.
  References: `Product/support/samples/pile-counter.md` §9.6 + §11 Q19; `Product/features/engine/models/06-game-definition.md` § Build walk.

- id: oq-request-timeout-primitive | kind: defer | **Framework-shipped request timeout primitive** — for networked / async / AFK-protected games, requests need a wall-clock deadline that auto-dispatches a `Cancelled = true` typed response on expiry. Locked design direction: `Request` base gains optional `TimeSpan? Timeout` (data-only, serializable); framework `TimerService` watches `RequestsSlice.Open`, schedules timers, and dispatches the cancelled response on expiry through the same pipeline as a player-driven cancel. Replay reproduces deterministically because the recorded `Response` envelope captures the cancellation regardless of source — the timer doesn't re-run during replay. Deferred to post-v1: cancellation primitive (`Cancelled` flag on Response) is already shipped per the spike Q-walk; games that need timeout can ship their own timer service against `RequestsSlice` in the meantime.
  Trigger: first networked-play game, AFK-protection requirement on a single-player game, or vertical-slice consumer that surfaces real friction.
  References: `Product/features/effects/models/06-request-system.md` (banner) + `Product/decisions/0011-effect-orchestration-dispatch-requests.md` (refresh banner).

## Features yet to scaffold

- id: bl-interaction-feature | kind: task | **`interaction` feature — gesture-to-intent layer** — `features/interaction/` folder and at least one Model doc needed. Translates raw input gestures (drag/drop/click/target) to typed `PlayerIntent` records. The consumer of `PlayerIntent` is per-state `MatchStateViewModel<TState>.Map(intent)` (per [ADR-0012](Product/decisions/0012-presentation-per-state-view-models.md) and [`features/presentation/models/01-match-state-view-model.md`](Product/features/presentation/models/01-match-state-view-model.md)) — VMs convert intents to `GameCommand` (incl. `Response` when in request-mode per [ADR-0011](Product/decisions/0011-effect-orchestration-dispatch-requests.md)). `PlayerIntent` ownership now lives in `presentation/` feature-root vocabulary.
  Trigger: next presentation or engine-wiring session.
  References: `Product/general/ARCHITECTURE.md`; `Product/decisions/0012-presentation-per-state-view-models.md`; `Product/decisions/0011-effect-orchestration-dispatch-requests.md`.

- id: bl-authoring-feature | kind: task | **`authoring` feature — effect recipe and card catalog models** — `features/authoring/` folder and at least one Model doc needed. Effect shape now constrained: `IEffect.Run(scope) : Task` (per [ADR-0011](Product/decisions/0011-effect-orchestration-dispatch-requests.md) and [`features/effects/models/01-effect.md`](Product/features/effects/models/01-effect.md)). Authoring vocabulary still open — how a card definition holds a per-instance effect recipe is for `oq-effect-authoring`.
  Trigger: after vertical slice exercises the runtime contract end-to-end.
  References: `Product/decisions/0011-effect-orchestration-dispatch-requests.md`; `Product/features/effects/models/01-effect.md`; `Product/general/ARCHITECTURE.md` §Authoring.

## Workflow

- id: bl-spike-status-migration | kind: task | **Reclassify existing spikes to the new status vocabulary** — the spike state machine was tightened to `draft / active / graduated / concluded`. Four existing spikes carry retired statuses: `domain-model-proposal.md` (`superseded`), `entities-bridge-replacement.md` (`superseded`), `entities-state-unification.md` (`accepted`), `state-object-management.md` (`open`). Each needs a per-spike call: `graduated` (informed a Model/ADR that has since landed) or `concluded` (closed without producing one). `effect-pipeline.md` is `draft` but lives in `spikes/`; per the new convention it should either move to `_drafts/` or flip to `active`.
  References: `Product/_workflow/PRODUCT-BUILDER.md` §Spike, `Product/_docs/spike.md`, `Product/spikes/`.

- id: bl-feature-rulebook-rebuild | kind: task | **Rebuild `_docs/feature.md` to match the model.md treatment** — feature.md is silent on whether the 8-section H2 order is strict. In practice 7 features follow it exactly, 2 follow it with extras inserted between Composition and Invariants (engine, triggers), and 1 (state) diverges substantially. Rebuild the rulebook with explicit section-order rules (likely strict-anchored-with-extras-allowed, matching de-facto convention), required-vs-optional sections, and an audit obligation clause. Then conform every feature root.
  References: `Product/_docs/feature.md`, `Product/_docs/model.md` (treatment to mirror), `Product/features/*/[a-z]*.md`.

- id: bl-model-conformance-pass | kind: task | **Conform existing Models to the rebuilt model.md template** — the Model rulebook was rebuilt for AI-direct format. The 22 existing Models need a per-doc audit + adjustment for shape compliance (section order, required sections, identity-test gates). Do alongside or after `bl-feature-rulebook-rebuild` so the cascade is one pass.
  References: `Product/_docs/model.md`, `Product/features/*/models/*.md`.

## Tooling

- id: bl-feature-map-generator | kind: task | **FEATURE-MAP auto-generator** — replace hand-maintained `FEATURE-MAP.md` with a generator that reads feature roots, Models, and `references:` front matter to produce the index table, mermaid graph, and model breakdown. Land alongside the broader graph-rendering vision (see `PLANS/simplify-doc-workflow.md` future considerations).
  Trigger: ~15 Models in `Product/features/` OR first drift incident traceable to a stale FEATURE-MAP.

- id: bl-drift-detection | kind: task | **`builder-drift-detection` skill** — session-mode + CI-mode drift detection between code in `Assets/` and `settled`/`implemented` Features/Models in `Product/`.
  Trigger: first feature reaches `implemented` status (§5.6.2 build gate).

- id: bl-definition-editor | kind: defer | **Definition Editor** — authoring tool for cards, zones, game templates, effects, visuals.
  Trigger: `authoring` feature reaches `settled`.
  References: `Product/general/ARCHITECTURE.md` §Authoring; `Product/ledgers/FEATURE-MAP.md` `authoring` (considered).

- id: bl-simulation-debugger | kind: defer | **Simulation Debugger** — inspector for state, history, pending tasks, triggers, decisions.
  Trigger: first feature reaches `implemented` (debugger needs a real running simulation to inspect).
  References: `Product/general/ARCHITECTURE.md` §Tooling.

- id: bl-scenario-runner | kind: defer | **Scenario Runner** — load a known state and exercise a command/effect for fast iteration.
  Trigger: first feature reaches `implemented`.
  References: `Product/general/ARCHITECTURE.md` §Tooling.

- id: bl-replay-viewer | kind: defer | **Replay Viewer** — step through history and inspect state transitions.
  Trigger: history pipeline (`engine/04-available-actions`) reaches `implemented`.
  References: `Product/features/engine/models/04-available-actions.md`; `Product/general/ARCHITECTURE.md` §Tooling.

- id: bl-rule-trace | kind: defer | **Rule Trace** — explain why an action was legal/illegal and which effects modified it.
  Trigger: first feature reaches `implemented`.
  References: `Product/general/ARCHITECTURE.md` §Tooling.

- id: bl-validation-reports | kind: defer | **Validation Reports** — detect broken card definitions, missing assets, bad IDs, invalid references.
  Trigger: `authoring` feature reaches `draft` (needs a definition surface to validate).
  References: `Product/ledgers/FEATURE-MAP.md` `authoring` (considered).

## Spatial / cards

- id: bl-spatial-rule-attachment | kind: defer | **Spatial rule attachment surface for zones** — when a game (Stacklands-style) needs spatial coordinates as rules data rather than presentation layout, the framework must commit to where spatial info attaches. The current zone contents shape is a flat `IReadOnlyList<Card>` — there is no pre-defined hook. When forced by a real game, decide between: extending `ZoneContentsState` with per-entry structure (slot/coord field), introducing a parallel `SpatialState` slice keyed by card, or per-card position variable.
  Trigger: first spatial-rules game (Stacklands-like) reaches design-pass.
  References: `Product/features/zones/models/01-zone-model.md` §Deferred (and the zone-shape provenance Q below the divider — formerly `zones/decisions/0001-zone-shape.md`, folded 2026-05-08).

- id: bl-card-location-index | kind: defer | **Reverse-lookup index for card → zone** — derived aggregate slice (e.g., `CardLocationIndex` keyed by `CardRef → Zone`) that answers "what zone is this card in?" in O(1) instead of scanning all `ZoneContentsState` slices. Maintained atomically by the same mutators that write `ZoneContentsState`. Originally drafted into the cards Model during session-20260501-1602 but removed after audit found no current consumer.
  Trigger: first reverse-lookup consumer surfaces (e.g., a Stacklands-style game's spatial query, a presentation feature that needs to render card-to-zone arrows, or any rule that needs frequent reverse lookup at scale).
  References: `Product/features/cards/models/01-card-model.md`, `Product/features/zones/models/01-zone-model.md` §Read API.

## Multiplayer / networking

- id: bl-versioned-definitions | kind: defer | **Versioned Definitions** — definition versioning/hashing for client-server agreement.
  Trigger: `networking` feature begins; or save/load arrives, whichever first.
  References: `Product/ledgers/FEATURE-MAP.md` `networking` (considered).

## Presets

- id: bl-presets-package | kind: defer | **Presets package** — preset rule templates (TCG, deckbuilder, spatial sim) as compositions of framework primitives.
  Trigger: post-v1, after at least 2 features reach `implemented` and the framework primitive surface is stable.
  References: `Product/general/ARCHITECTURE.md` §Preset Direction; `Product/general/archive/RECOMMENDATIONS.md` §Recommendation 7 (archived).

## Tier-3 area questions (not promoted to specific entries yet)

The following areas have unresolved questions but no individual entry has been promoted with a specific design surface to chase. Promote into a self-contained entry when one becomes load-bearing.

**Authoring** — how effects are authored (data / code / graphs / mix); smallest useful effect language; simple cards easy / complex cards possible; visuals/sounds/animations coupling; editor validation; definition versioning for save/load and multiplayer.

**Presentation** — common card presentation contract (UI, 2D, 3D); animation consumption (raw history vs presentation events vs animation timeline); visual state outside canonical simulation; animation lag; legal-target / drop-destination input; failed-action presentation when gesture was visually allowed.

**Multiplayer** — minimum command envelope for transport; minimum history envelope for replay/transport; visibility filtering location (core / adapter / both); client filtered-Store vs presentation-only; reconnect snapshot generation; definition mismatch detection; rollback/prediction need.

**Scaffold integration** — MVVM models generated from sim state/history vs subscribed to slices; AutoPacker introduction timing.

**Presets** — common core vs TCG preset boundary; deckbuilder vs generic zone/effect boundary; spatial-sim preset vs generic drag/stack; preset composition vs exclusive modes; per-game preset overrides without forking.

**Testing & tooling** — first testable scenario; determinism in tests; assert final state vs full history vs both; human-readable scenario snapshots; pre-effect-work debug tools; "why was this action legal/illegal?" tracing.

**First-game target** — vertical-slice game preset still unchosen.

## Explicit non-goals

These have been actively decided against. Listed here so they don't get accidentally re-opened.

- **True simultaneous resolution** — measuring all competing effects against a state snapshot and applying merged deltas atomically is off the table. It requires a scratch-pad model and introduces irreconcilable conflicts. "Simultaneous" always means sequential in a game-defined order.
