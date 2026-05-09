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

- id: oq-move-validation-execution-drift | kind: question | **Move validation-vs-execution state drift** — when a `MoveCardMutation` (or any mutation with state preconditions) is validated and queued, then another mutation moves the card before the original executes, the original's source precondition fails at execute-time. Current behavior (per Q14 lock, session-20260501-1602): throw at execution. Other strategies to evaluate when this becomes painful: re-resolve source from current state, silent no-op, or deeper interlocking via the trigger-fold / replacement-effects pipeline. Affects all mutations with state preconditions, not just MoveCard.
  References: `Product/features/cards/models/01-card-model.md`, `Product/features/zones/models/01-zone-model.md`.

- id: oq-effect-pipeline | kind: question | **Effect-pipeline shape** — per [ADR-0006](decisions/0006-two-pipeline-architecture.md), the runtime needs an effect-level orchestration surface that decides *when each effect starts* (immediate, batched, stacked). Working sketch in [`spikes/effect-pipeline.md`](spikes/effect-pipeline.md) — `IEffectPipeline` injected service, `EffectDefinition` + `EffectInstance` split, chain scope per effect-run, two execution verbs. Iterating; not yet promoted to canonical Models. Resolution path lives in the spike's "When this lands" section.
  References: `Product/spikes/effect-pipeline.md`, `Product/decisions/0006-two-pipeline-architecture.md`, `Product/features/engine/models/03-game-rules-runtime.md`.

- id: oq-effect-authoring | kind: question | **Effect authoring vocabulary** — `EffectDefinition.Execute` is currently a delegate (`Func<EffectInstance, IEffectScope, Task>`); the *authoring vocabulary* — closed-set primitives ("deal damage / draw N / apply modifier") composed into effects, free-form code, graph-shaped, hybrid — is unsettled. Tied to the broader `bl-authoring-feature` task. Surfaces concretely once the effect-pipeline sketch promotes to Models.
  References: `Product/spikes/effect-pipeline.md`, `Product/features/effects/models/04-command-awaiter.md`.

- id: oq-runtime-as-state-evaluator | kind: defer | **Runtime collapses toward pure state evaluator** — the runtime contract has progressively shrunk (`Route`/`OnIncomingCommand`/`OnQueueDrained`/`IIntakeQueue` deleted; today: `FilterTriggers + Evaluate`). Long-term direction surfaced during the effect-pipeline sketch: `FilterTriggers` is the next candidate to revisit, and the runtime might collapse to pure state evaluation + state-machine transitions. Not actionable now; revisit after effect-pipeline lands and at least one game stresses the surface.
  Trigger: post-effect-pipeline cascade + first composed-rules game.
  References: `Product/spikes/effect-pipeline.md`, `Product/features/engine/models/03-game-rules-runtime.md`, `Product/decisions/0006-two-pipeline-architecture.md`.

- id: oq-session-state-machine | kind: defer | **Session-level state machine** — current `EngineLifecycle` is 3 states (`NotStarted` / `Started` / `Ended`). Real applications need richer states: `Connecting`, `Reconnecting`, `ShowingResults`, `SettingUp`, etc. The runtime is well-positioned to host a richer state machine alongside `Evaluate`. Pin when the first networked or session-aware game forces it.
  Trigger: first networked game, or first game that needs post-match flow (results screen, rematch lobby, etc.).
  References: `Product/features/engine/models/01-game-engine.md`, `Product/features/engine/models/01-game-engine.md`.

- id: oq-gamerules-composition | kind: question | **`GameRulesRuntime` composition** — a single `GameRulesRuntime<TDefinition>` implementation is shown; in practice it may be composed of smaller rule modules. `IAvailableActions` service surface (`Set`/`Append`/`Clear`) supports module composition mechanically. Modular rules likely double-generic on `(TDefinition, TModuleConfig)` and compose alongside the central runtime. The composition contract (provider/policy ordering, enable/disable, sub-module DI surface) is deliberately undesigned for now.
  References: `Product/features/engine/models/03-game-rules-runtime.md` §Composition.

- id: oq-fsm-game-state | kind: question | **FSM-shaped game state** — `FreeInput`/`DecisionPrompt` were deleted (S15 Q6); snapshot carries templates only. Type-only routing in `ICommandAwaiter` is the current mechanism for recognizing in-progress decision commands. The FSM-shaped state model (typed `PickATargetState`, `MainPhaseState`, etc., each owning their own snapshot-generation surface) is the eventual successor. Revisit when the first async-decision-heavy game arrives.
  References: `Product/features/effects/models/04-command-awaiter.md` §9.

- id: oq-trigger-ordering | kind: question | **Trigger ordering within fold** — what determines sequence when multiple triggers are registered for the same type: FIFO by registration, priority weight, game-configurable, or something else.

- id: bl-actor-ownership-reverse-index | kind: defer | **Reverse-direction lookup on `OwnershipState`** — `OwnershipState` is one-way today (`Reference → IActor`). Reverse queries ("what does this player own?") are O(N) filters or game-side aggregates. When a consumer surfaces (e.g., a presentation view that renders all of a player's cards, a rule that needs O(1) reverse lookup at scale), extend `OwnershipState` with a parallel reverse index (`IActor → ImmutableSet<Reference>`) — same two-way pattern as `CardMapState`. Surfaced by Q5 of the domain modeling design pass (2026-05-06).
  Trigger: first reverse-lookup consumer surfaces.
  References: `Product/features/engine/models/06-game-definition.md` § Ownership and zone assignment; `Product/decisions/0005-objects-over-ids.md`.

- id: oq-effect-cancellation-on-match-end | kind: question | **Async-effect cancellation on match end** — when `EndMatchCommand` resolves while an effect is mid-`await` (target picker, choose-one prompt, chained dispatch), the awaiting effect is still alive. Engine needs a story for cancellation: thread a `CancellationToken` through `IHandlerContext` / `IEffectScope`, hard-cancel via `IDisposable`, or accept that orphaned awaiters resolve into a closed match. Surfaced from the engine match-lifecycle design pass; deferred from that pass because it touches the effect contract, not lifecycle.
  References: `Product/features/effects/models/01-effect.md`, `Product/features/effects/models/04-command-awaiter.md`, `Product/features/engine/models/01-game-engine.md`.

- id: oq-intent-template-lookup | kind: question | **Intent → template lookup** — `IIntentMapper.Map(ActionTemplate, PlayerChoice)` requires a pre-resolved template, but how the gateway/store picks the matching `ActionTemplate` from the snapshot for a raw `PlayerIntent` is undocumented. The "(or builds one from the intent)" clause in presentation/01 §10 step 6 is unexplained.
  References: `Product/features/presentation/models/01-presentation-loop.md` §8, §10.

- id: oq-trigger-subscription-entry | kind: question | **Trigger subscription entry point for regular cards** — `effects/03 §6` shows a handler calling `AbilityDefinition.Register` for attachments. For regular cards entering play (zone entry), no system or lifecycle hook is shown that calls `Register`. Tied to game lifecycle and zone-entry events.
  References: `Product/features/triggers/models/01-trigger-fold.md` §5.

- id: oq-history-envelope | kind: question | **`HistoryEnvelope` typed definition** — currently pseudocode with named fields (`envelopeId`, `originCommandId`, `actor`, `entries[]`); no typed C# record exists. `EnvelopeCause` is glossary-named but undefined. Load-bearing for replay, multiplayer transport, and the presentation history loop.
  References: `Product/features/actions/models/01-command-pipeline.md` §History Envelope.

- id: bl-zone-visibility-custom-policy-serialization | kind: defer | **`ZoneVisibilityPolicy.Custom` serialization** — the named `ZoneVisibilityPolicy` cases (`AllPublic`, `AllHidden`, `OwnerSees`, `OwnerSeesOthersCount`, `OwnerCountOthersNothing`) round-trip cleanly through replay/save/network. The `Custom(Func<IActor, IActor?, VisibilityResult>)` escape hatch carries an arbitrary delegate that breaks serialization. Story: either (a) replace `Custom` with a registered-by-id resolver (game registers an `IZoneVisibilityResolver` keyed by id; `Custom(string ResolverId)` records the id), (b) drop `Custom` and force games to add a slice-based override mechanism, or (c) accept that `Custom` games can't replay/network. Surfaced by Q7 of the open-seams design pass (2026-05-06).
  Trigger: first game uses `Custom`, OR networking/replay design forces serialization rules.
  References: `Product/features/zones/models/01-zone-model.md` § Zone visibility policy.

- id: bl-card-resolution-helper-shape | kind: defer | **CardRef → Card resolution helper location** — the two-hop resolution (`CardRef → CardMapState → Ref<Card> → Catalog → Card`) needs a single helper to avoid call-site verbosity. Candidate shapes: extension method on `Store` (`store.ResolveCard(cardRef)`), instance method on `CardRef` (`cardRef.Resolve(store)`), shared utility class. All work; choice is ergonomic. Surfaced by Q6 of the open-seams design pass (2026-05-06); deferred to code time.
  Trigger: first concrete implementation requires picking a shape.
  References: `Product/features/cards/models/01-card-model.md` § Resolution surfaces.

- id: bl-pile-refs-index | kind: question | **Engine-side index slice for spawned per-game zone references** — `PileCounterGameDefinition.PileRefsAt(state)` (and its analog for any game with a fixed set of game-shared zones) needs an engine-side index slice (`PileIndexState` or similar generic shape — `Dict<TGameKey, Ref<Zone>>` or `IReadOnlyList<Ref<Zone>>`) populated during the factory's Build walk so the runtime and intent mapper can enumerate spawned zone refs without scanning `OwnershipState` or filtering by definition type. Open question: framework-provided generic index, game-side helper, or some intermediate. Surfaced by the PileCounter sample refresh (2026-05-06).
  Trigger: first vertical-slice implementation needs concrete enumeration of game-shared zones spawned at Build.
  References: `Product/support/samples/pile-counter.md` §9.6 + §11 Q19; `Product/features/engine/models/06-game-definition.md` § Build walk.

## Features yet to scaffold

- id: bl-interaction-feature | kind: task | **`interaction` feature — gesture-to-intent layer** — `features/interaction/` folder and at least one Model doc needed. The presentation loop assumes this layer converts drag/drop/click gestures to `PlayerIntent`, but no model, no folder, and no ADR exists. All intent vocabulary defined in `features/actions/models/01-command-pipeline.md` (Q5 — formerly `actions/decisions/0008-intent-vocabulary.md`, folded 2026-05-08) depends on this layer to emit the intents.
  Trigger: next presentation or engine-wiring session; or whenever intent → template lookup is designed.
  References: `Product/general/ARCHITECTURE.md`, `Product/features/actions/models/01-command-pipeline.md` §Q5 (provenance), `Product/features/presentation/models/01-presentation-loop.md` §8.

- id: bl-authoring-feature | kind: task | **`authoring` feature — effect recipe and card catalog models** — `features/authoring/` folder and at least one Model doc needed. `effects/04-command-awaiter.md (effect recipe still open)` defers how a card definition holds a per-instance `Effect` recipe to "a future authoring Model." No model, no folder, no ADR, no spike exists.
  Trigger: after slice catalog and game lifecycle land; before any vertical-slice implementation.
  References: `Product/features/effects/models/04-command-awaiter.md` §3, §9; `Product/general/ARCHITECTURE.md` §Authoring.

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
  References: `Product/general/ARCHITECTURE.md` §Preset Direction; `Product/general/RECOMMENDATIONS.md` §Recommendation 7.

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
