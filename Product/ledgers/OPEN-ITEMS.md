---
type: ledger
tags: [#open-items]
---

# Open Items

Single ledger of loose ends — durable design questions, deferred work, and known TODOs that aren't in flight right now. Each entry is self-contained: reading any single one tells the full story (what's known, what's open, why it matters, where to look).

**Resolution = delete the entry.** The resolving doc owns the answer (ADR, spike, `docs/architecture.md`, roadmap phase). Git keeps deletion history if ever needed: `git log --diff-filter=D ledgers/OPEN-ITEMS.md`.

**Optional `kind:`** per entry — `question` (we don't know yet), `task` (we know what to do, deferred), `defer` (decided not now, revisit on trigger).

**Vocabulary.** Entries use the terms in [`GLOSSARY.md`](Product/ledgers/GLOSSARY.md) and the model in [ADR-0014](Product/decisions/0014-service-first-state-model.md). Most entries name the [roadmap](Product/spikes/framework-roadmap.md) phase that owns them; that phase's spike resolves the entry.

> **Swept 2026-10-18 (roadmap phase 0).** Entries written for the old slice-based model were closed, rewritten, or pointed at a roadmap phase. Closed in this sweep:
> - `oq-history-envelope` — answered by ADR-0014 §4: the `EventStream` is the history; `BlockStartEvent` / `BlockEndEvent` mark commands. Block payload and source capture are in the roadmap's *Not scheduled* list.
> - `oq-gamerules-composition` — the rules-runtime object it asked about is not built and not planned. The question that survives (how game states compose and who picks the next state) is `oq-state-flow` below.
> - `oq-effect-save-load` — tracked in the roadmap's *Not scheduled* list (mid-effect save/load is not supported by design).
> - `bl-card-resolution-helper-shape` — answered by code: `Entity` carries `DefinitionId`, and `Catalog<TDef>` looks the definition up. There is no reference type to resolve.
> - `bl-pile-refs-index` — the index slice it discussed is retired, and no consumer needs it. Zone ordering is designed in phase 3.
> - `bl-interaction-feature` — owned by roadmap phase 2 (`Submit`) and phase 7 (gesture → intent).
> - `bl-authoring-feature` — owned by roadmap phase 6. The open part stays as `oq-effect-authoring`.
> - `bl-feature-rulebook-rebuild`, `bl-model-conformance-pass`, `bl-feature-map-generator` — every feature root and Model was archived on 2026-10-18, so there is nothing to conform or index. The question that remains is `oq-product-features-role`.

---

## Engine + runtime

- id: oq-state-flow | kind: question | **How game states compose and who picks the next state** — `StateService.Push<TState>()` replaces `StateStore.Current`; there is no stack, no pop, no OnEnter/OnExit, and nothing that decides the successor state. ADR-0009 D1 (engine is the FSM), D3 (state is a predicate with no data) and D4 (successor knowledge, `IStateFlow.NextAfter` in the spikes) survive as unbuilt designs per [ADR-0014](Product/decisions/0014-service-first-state-model.md#effect-on-earlier-adrs). Open: one flow object or composed rule modules; whether states need a stack (for nested phases); where enter/exit logic lives.
  Owner: roadmap phase 1 (engine loop).
  References: `Product/spikes/state-service.md`, `Product/spikes/framework-architecture.md`, `Product/decisions/0009-engine-as-fsm.md`.

- id: oq-system-gate-validation | kind: question | **System-gate validation in the engine loop** — today each command has at most one `CommandValidator<T>`, and nothing checks whether a command is legal in the current `GameState` or while the match is not accepting commands. The roadmap moves this check into the engine loop. Open: (a) is the gate a framework default that runs before every `CommandValidator<T>`, or a validator that games register; (b) how a `GameState` declares which commands it allows; (c) whether more framework defaults join it (disposal guard, re-entrancy guard). ADR-0007's layered-validation principle survives as the design input.
  Owner: roadmap phase 1 (engine loop).
  References: `Product/decisions/0007-five-validation-layers.md`, `Product/decisions/0010-validator-handler-split.md`, `Product/spikes/command-service.md` § Deferred items.

- id: oq-pump-policy | kind: question | **When the engine loop pumps, waits, or stops** — phase 1 adds a queue and a pump that runs one item at a time. Open: what the pump does while a `Request` is open (block, or keep running unrelated items); whether the current `GameState` can hold the pump; where state-based actions (phase 4) are checked between items; how games override the policy. Decide inside the phase 1 spike; promote to its own doc only if game overrides start to compose in non-trivial ways.
  Owner: roadmap phase 1 (engine loop); SBA part in phase 4.
  References: `Product/decisions/0013-engine-pipeline-orchestration.md`, `Product/decisions/0011-effect-orchestration-dispatch-requests.md`.

- id: oq-entity-destruction | kind: question | **Entity destruction lifecycle** — `EntityService` can only `Create`; there is no destroy event, and `TagStore` keeps tag values for an id forever. Open: what event destroys an entity and which apply handlers react (tags, zone membership, open requests that name it); whether a destroyed id can be reused; what happens to queued commands or awaiting effects that hold the id. Load-bearing for ongoing effects and modifiers that remove themselves.
  Owner: roadmap phase 3 (event and stores); phase 1 decides ids vs live `Entity` refs on queued commands.
  References: `Assets/Framework/Entities/Runtime/EntityService.cs`, `docs/architecture.md` § Not built yet.

- id: oq-attachment-storage | kind: question | **Where attachments live** — equipment, auras and buffs attach to a host entity. With modifiers as entities, the likely shape is an attachment entity that points at its host through a tag. Open: host tag vs a zone on the host vs a dedicated store; whether destroying the host destroys its attachments (see `oq-entity-destruction`).
  Owner: roadmap phase 5 (modifiers).
  References: `Product/spikes/modifiers-as-entities.md`.

- id: oq-effect-authoring | kind: question | **Effect authoring vocabulary** — no effect type exists in `Assets/Framework/` yet. ADR-0011's async-coroutine shape (an effect can `await` a `Request`) survives as the design input for phases 1 and 4. Open: closed-set primitives ("deal damage", "draw N", "apply modifier") composed into effects, free-form C#, graphs (`com.scaffold.graphflow`), or a mix; how a card definition in `Catalog<TDef>` holds the recipe that builds an effect at fire time.
  Owner: roadmap phase 6 (card definitions + authoring); runtime shape in phase 4.
  References: `Product/decisions/0011-effect-orchestration-dispatch-requests.md`, `Packages/com.scaffold.graphflow/FOLLOW-UPS.md`.

- id: oq-trigger-ordering | kind: question | **Trigger ordering** — when several triggers react to the same event, what decides their order: registration order (FIFO), a priority weight, a game-configured rule, or the active player's choice through a `Request`.
  Owner: roadmap phase 4 (reactions).
  References: `Assets/Spike/ECS/VerticalSlice/` (reference behavior).

- id: oq-trigger-subscription-entry | kind: question | **When a card's triggers start listening** — a card's triggers must become active when it enters play (a zone change) and stop when it leaves. Nothing defines the hook that registers or removes them. Depends on the zone store (phase 3).
  Owner: roadmap phase 4 (reactions).
  References: `Assets/Spike/ECS/VerticalSlice/` (Deathrattle).

- id: oq-targeting-pre-play | kind: defer | **Pre-play targeting** — a card whose play needs a target chosen *before* the play command runs (the target is a field on the command, not a `Request` inside the resolution). Likely a `PlayCard` command with a target field and a `CommandValidator<T>` that checks it. Mid-effect targeting uses `RequestService` instead.
  Trigger: first game with play-time targets (Magic-style auras, choose-mode cards).
  Owner: roadmap phase 4 or 6, whichever first meets such a card.

- id: oq-request-timeout | kind: defer | **Request timeout** — networked, async or AFK-protected games need a deadline on a `Request` that answers it automatically when time runs out. Today `RequestService.CancelAllPending()` cancels every pending task, and `Response` carries only `RequestId` — there is no per-request cancel or timeout. Direction: an optional serializable timeout on `Request`; a server timer that sends the default or cancel answer through the same path as a player answer, so the stream records it and replay does not re-run the timer.
  Trigger: first networked game, an AFK requirement, or friction in a vertical slice.
  Owner: roadmap phase 2 (response round trip) builds the path this needs.
  References: `Assets/Framework/Requests/Runtime/RequestService.cs`, `Product/spikes/request-service.md`.

- id: oq-legal-actions-shape | kind: question | **How a client learns which actions are legal** — the client has stores but no services or validators, so it cannot ask "is this command legal?". Presentation (highlight playable cards), AI and replay tools need an answer. Open: does the server emit a legal-actions event per decision point, does the client run a read-only copy of the validators, or is legality only checked on `Submit` and reported back as a rejection; and how rich the shape is (command type only, or targets, modes and parameters too).
  Owner: roadmap phase 2 (`Submit`); presentation use in phase 7.
  References: `Product/decisions/0012-presentation-per-state-view-models.md`, `Product/spikes/acceptance-test-suite.md` § Deferred ladder.

- id: oq-view-model-host-shape | kind: question | **View-model base shape** — ADR-0014 keeps per-state view-models dispatched on the `GameState` record type (`GameStateViewModel<TState>`). The base class shape (animation hooks, observable wiring, disposal, lifecycle) depends on the host MVVM toolkit, which is not chosen. Pin it when phase 7 picks the toolkit.
  Owner: roadmap phase 7 (presentation). ADR-0012 needs a review first.
  References: `Product/decisions/0012-presentation-per-state-view-models.md`, `Product/spikes/card-presentation.md`.

## Board: zones, players, ownership

- id: oq-hidden-information | kind: question | **Hidden information on the wire** — `WireForwarder` sends every server event to every client, so a client could read a hand or deck it should not see. Zones need visibility rules (all public, all hidden, owner sees, others see a count), and the wire needs per-client filtering or redaction. Open: filter whole events per peer, redact fields, or send per-peer copies; how replay works with redacted events; where the rule is declared (zone definition vs a registered resolver). An arbitrary-delegate rule cannot be serialized, so rules should be data or a registered id.
  Owner: roadmap phase 3 (zone visibility); the wire part needs phase 2.
  References: `Product/decisions/0008-zone-visibility-model.md`, `Product/spikes/network-architecture.md`, `Product/spikes/multiplayer-architecture.md`.

- id: oq-card-location-lookup | kind: question | **Card → zone lookup** — the zone store needs ordered membership per zone. Open: whether the card also carries its zone as a tag (O(1) "where is this card?") kept in step by the same apply handler, or whether the reverse lookup scans zones. Decide while designing the zone store, based on a real consumer.
  Owner: roadmap phase 3 (board).
  References: `Assets/Spike/ECS/VerticalSlice/` (zones).

- id: bl-ownership-reverse-index | kind: defer | **Reverse lookup for ownership** — ownership and controller will be tags on entities (phase 3). "What does this player own?" is then a scan over `TagStore`. Add a reverse index only when a consumer needs it at scale (a view that shows all of a player's cards, a rule that queries it often).
  Trigger: first reverse-lookup consumer.
  Owner: roadmap phase 3 (board).

- id: bl-spatial-rule-attachment | kind: defer | **Spatial data as rules data** — a Stacklands-style game needs positions that rules read, not only layout. Decide when a real game needs it: position tags on cards, a per-zone slot or coordinate on membership entries, or a separate store.
  Trigger: first spatial-rules game reaches design.
  Owner: after roadmap phase 3 (board).

## Product docs and workflow

- id: oq-product-features-role | kind: question | **What `Product/features/` is for now** — every feature root and Model was archived on 2026-10-18. Current design lives in `docs/architecture.md`, the ADRs and the spikes linked from the roadmap; `docs/feature-layout.md` describes features in code. Open: do new features get a `Product/features/<name>/` root again (and then `_docs/feature.md` and `_docs/model.md` need a rebuild, and `FEATURE-MAP.md` a generator or retirement), or do spikes plus `docs/architecture.md` replace that layer for good?
  Trigger: the first roadmap phase that wants a durable design doc beyond its spike (likely phase 1).
  References: `Product/_docs/feature.md`, `Product/_docs/model.md`, `Product/ledgers/FEATURE-MAP.md`, `docs/feature-layout.md`.

- id: bl-spike-status-migration | kind: task | **Reclassify spikes to the status vocabulary** — the spike states are `draft / active / graduated / concluded`. Spikes still carry other values: `superseded` (`domain-model-proposal.md`, `entities-bridge-replacement.md`, `match-state-redesign.md`), `accepted` (`entities-state-unification.md`, `framework-architecture.md`), `open` (`state-object-management.md`), `locked` (`multiplayer-architecture.md`, `network-architecture.md`), `validated` (`entity-store-extraction.md`), `done` (`step3-entity-phases.md`), `deferred` (`hook-config-design.md`, `wire-network-protocol.md`). Each needs a call: `graduated` (informed code or an ADR), `concluded` (closed without one), `active`, or archive. Spikes that still describe the old model as current (`match-state-redesign.md`, `runtime-walkthrough.md`, `support/samples/pile-counter.md`) should be archived in the same pass.
  References: `Product/_workflow/PRODUCT-BUILDER.md` §Spike, `Product/_docs/spike.md`, `Product/spikes/`.

## Tooling

- id: bl-drift-detection | kind: task | **Drift check between code and docs** — `docs/architecture.md` says "when code and this doc disagree, the code is correct". A session or CI check could flag type names in `docs/architecture.md`, `GLOSSARY.md` and active spikes that do not exist in `Assets/Framework/`, plus retired tokens (`/builder:lint` T1 covers the second part).
  Trigger: next drift incident, or when CI for EditMode tests lands (roadmap phase 0, optional).

- id: bl-definition-editor | kind: defer | **Definition editor** — authoring tool for cards, zones, game templates and effects. Depends on the phase 6 decision on `com.scaffold.graphflow`.
  Trigger: roadmap phase 6 picks the authoring vocabulary.

- id: bl-simulation-debugger | kind: defer | **Simulation debugger** — inspect stores, the event stream and its blocks, the engine queue, open requests and active triggers in a running match.
  Trigger: roadmap phase 1 lands (there is a queue to inspect).

- id: bl-scenario-runner | kind: defer | **Editor scenario runner** — load a known state and run a command or effect for fast iteration outside tests. Acceptance tests already do this through `MatchHarness` and scenario pictures; the open part is an editor or play-mode tool for designers.
  Trigger: roadmap phase 6 (designers author cards).
  References: `docs/testing.md`.

- id: bl-replay-viewer | kind: defer | **Replay viewer** — step through the `EventStream` and inspect store state at each block. Stepping backwards needs rollback (`WireEventHandler.Unapply` throws today; roadmap *Not scheduled*).
  Trigger: rollback is scheduled.

- id: bl-rule-trace | kind: defer | **Rule trace** — explain why an action was legal or illegal and which effects or modifiers changed a value.
  Trigger: roadmap phases 1 (system-gate validation) and 5 (modifiers) land.

- id: bl-validation-reports | kind: defer | **Validation reports** — detect broken card definitions, missing assets, bad ids and invalid references.
  Trigger: roadmap phase 6 adds concrete definitions to `Catalog<TDef>`.

## Multiplayer / networking

- id: bl-versioned-definitions | kind: defer | **Versioned definitions** — version or hash definitions so server and client agree on card data.
  Trigger: roadmap phase 6 (concrete definitions) or save/load, whichever first; needed before networked play ships.

## Presets

- id: bl-presets-package | kind: defer | **Presets package** — preset rule templates (TCG, deckbuilder, spatial sim) built from framework parts.
  Trigger: after roadmap phase 8, when the framework surface is stable.

## Tier-3 area questions (not promoted to specific entries yet)

The following areas have unresolved questions but no individual entry has been promoted with a specific design surface to chase. Promote into a self-contained entry when one becomes load-bearing.

**Authoring** (phase 6) — smallest useful effect language; simple cards easy, complex cards possible; coupling to visuals, sounds and animations; editor validation.

**Presentation** (phase 7) — common card presentation contract (UI, 2D, 3D); whether views animate from raw events, from block brackets, or from a separate presentation timeline; visual state outside the simulation; animation lag behind the stores; feedback when a gesture looked legal but the command was rejected. View-models subscribe to client stores; whether they also read the `EventStream` is open.

**Multiplayer** (phase 2) — reconnect (snapshot vs replaying the stream); definition mismatch detection; whether prediction or rollback is needed. Hidden information is `oq-hidden-information`.

**Presets** — common core vs TCG preset boundary; deckbuilder vs generic zone and effect boundary; preset composition vs exclusive modes; per-game overrides without forking.

**Testing & tooling** — acceptance tests assert final store state from scenario pictures; whether some scenarios should assert the event stream (block order, emitted events) is open. Determinism across server and client is roadmap phase 2 (L+2 convergence).

**First-game target** — no shipping game chosen. Roadmap phase 4 uses the `VerticalSlice` Deathrattle scenario as its acceptance target.

## Explicit non-goals

These have been actively decided against. Listed here so they don't get accidentally re-opened.

- **True simultaneous resolution** — measuring all competing effects against a state snapshot and applying merged deltas atomically is off the table. It requires a scratch-pad model and introduces irreconcilable conflicts. "Simultaneous" always means sequential in a game-defined order.
