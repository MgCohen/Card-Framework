---
type: checkpoint
tags: [#checkpoint]
---

# Session Checkpoint

**Live spine.** This is the single source of truth for "where we are." Updated continuously as work happens. No open/close lifecycle.

---

## Where we stopped

**FSM-arch consolidation landed (2026-05-11).** Post-cascade cleanup PR (items 1–4 of the consolidation plan). Archived [`spikes/fsm-architecture.md`](spikes/archive/fsm-architecture.md) as `status: superseded` with `superseded_by` listing ADR-0009 / 0010 / 0011 / 0012 and an archive banner pointing readers to the canonical Models for each D-section. Swept GLOSSARY's 8 spike-D references (`GameRulesRuntime`, `EngineLifecycle`, `MatchState`, `MatchStateSlice`, `TransitionStateCommand`, `ITurnFlow`, `ISBA`, `SessionState`) to the landed ADRs + canonical Models. Swept OPEN-ITEMS spike-D references in `oq-validator-defaults` / `bl-zone-visibility-custom-policy-serialization` / `bl-interaction-feature` / `bl-authoring-feature` to ADR-0010 / 0011 / 0012. Repointed all `spikes/fsm-architecture.md` path refs across ADRs 0009–0012 + Models to `spikes/archive/fsm-architecture.md`; updated the four ADRs' spike-provenance footnotes to past-tense. FEATURE-MAP cycle observation rewritten to record the fully-dissolved `actions ↔ engine` cycle post-cascade. **`general/ARCHITECTURE.md` rewrite + `support/samples/pile-counter.md` rewrite deferred to follow-up PRs.**

**ADR-0012 — Presentation per-state ViewModels landed (2026-05-11).** Sub-ADR D — the final sub-ADR of the FSM-arch cascade. [`decisions/0012-presentation-per-state-view-models.md`](decisions/0012-presentation-per-state-view-models.md) ratifies spike D6–D9: per-state polymorphic `MatchStateViewModel<TState>` carrying `Bind(state, scope)` + `Map(intent) → GameCommand` (D6); `ViewModelRegistry` maps `MatchState` type → factory (D7); per-VM `Map(intent)` replaces global `IIntentMapper` (D8); `AffordanceMap` + global `IIntentMapper` + `ActionTemplate` typed-descriptor wire form + `PlayerChoice` retire (D9). Cascade: two new Models in `presentation/` (`01-match-state-view-model` + `02-view-model-registry`); archive of `presentation/01-presentation-loop.md`, `actions/02-intent-mapper.md`, `actions/05-available-actions-snapshot.md`; rewritten `presentation/presentation.md` feature root; patched `actions/actions.md` + `actions/01-command-pipeline.md`; engine Models 01 + 03 + 07 `[ADR-0012 — *forthcoming*]` flags re-pointed to landed ADR; FEATURE-MAP actions row 5→3 + presentation row 1→2; GLOSSARY refreshed; OPEN-ITEMS `bl-interaction-feature` refreshed; new `oq-view-model-host-shape`. **FSM-arch cascade complete.**

**ADR-0011 — Effect orchestration + dispatch + requests landed (2026-05-11).** Sub-ADR C of the FSM-arch cascade. [`decisions/0011-effect-orchestration-dispatch-requests.md`](decisions/0011-effect-orchestration-dispatch-requests.md) ratifies spike D13–D17: only the engine fires effects (D13; rolls back the 2026-05-08 Q1 unification verdict); the `GameCommand` family expands to five siblings (`GameAction` / `CompositeAction` / `GameSignal` / `Request<TResponse>` / `Response`) with a single async `Dispatch<TResult>(GameCommand)` surface on `IEffectScope` (D14); `PendingEffectsSlice` is the universal effect queue (D15); effects are async coroutines via `IEffect.Run(scope) : Task` (D16); player decisions ride a state-linked request system (`RequestsSlice` + `AnswersSlice` + `IRequestService` + `RequestId`, D17). Cascade: two new Models (`effects/05-pending-effects`, `effects/06-request-system`); heavy rewrites of `effects/01-effect.md` + `effects/02-effect-scope.md`; archive of `effects/04-command-awaiter.md` + `spikes/effect-pipeline.md`; patches to `actions/01-command-pipeline.md`, `engine/01-game-engine.md` (drain Phase 2), `engine/03-game-rules-runtime.md` (drain-policy guards), feature roots; FEATURE-MAP + GLOSSARY + OPEN-ITEMS refreshed; `oq-effect-pipeline` closed.

**ADR-0010 — Validator / handler split landed (2026-05-11).** Sub-ADR B of the FSM-arch cascade. [`decisions/0010-validator-handler-split.md`](decisions/0010-validator-handler-split.md) ratifies spike D10–D11: `ICommandValidator<TCommand>` splits from `ICommandHandler<TCommand, TResult>`; `GameCommandHandler<,>` and `CommandValidator<>` abstract bases land with `[Inject] protected Store`; `ValidatedCommandHandler<,>` combined base covers the common case; validators are uniform across every `GameCommand` subtype; short-circuit on first reject, DI registration order = run order; `IHandlerContext` retired (archived); ADR-0009's inline two-layer intake gate converts to two registered framework-default validators `SessionGate` + `MatchStateActionGate`. Cascade: new Model `actions/06-command-validator.md`; heavy patch to `actions/03-command-handler.md`; archive of `effects/03-handler-context.md`; patches to `actions/01`, `actions/02`, `effects/02`, `engine/01`, `engine/03`, `engine/07`, `entities/05`; feature roots; ADR-0007 amendment; FEATURE-MAP + GLOSSARY + OPEN-ITEMS refreshed.

**ADR-0009 — Engine as FSM landed (2026-05-10).** First sub-ADR of the FSM-arch cascade per the agreed decomposition (4 sub-ADRs by axis: A engine-as-FSM, B validator/handler split, C effect orchestration + dispatch + requests, D presentation per-state VMs). [`decisions/0009-engine-as-fsm.md`](decisions/0009-engine-as-fsm.md) ratifies spike D1–D5 + D12: engine IS the FSM (`Evaluate → MatchState`); `MatchStateSlice` wraps polymorphic `MatchState`; states are data + `ShouldExit`; successor knowledge in `ITurnFlow.NextAfter`; cross-cutting overrides via `ISBA`; session lifecycle is typed sealed `SessionState` on `engine.Session` field. Cascade: three new Models (`engine/07-match-state`, `engine/08-turn-flow`, `engine/09-state-based-actions`); heavy patches to `engine/01-game-engine` (replaced `EngineLifecycle` enum with `SessionState` typed hierarchy; intake gate now two-layer — `Session` + `MatchState.Actions`; available-actions snapshot section removed; `EngineState.Lifecycle` field retired) and `engine/03-game-rules-runtime` (`Evaluate(action, result) → MatchState`; `Flow` + `StateBasedActions` abstract properties; default `Evaluate` body composes SBAs + ShouldExit + Flow; `IAvailableActions` removed from injected services; example Pile Counter runtime updated); `engine/04-available-actions` archived to `engine/archive/`; engine feature root rewritten (Composition 6→8, framework-slice catalog swaps `AvailableActionsState` for `MatchStateSlice`); FEATURE-MAP engine row 6→8 with archive note; cross-feature refs in `actions/05`, `presentation/01` get banners flagging upcoming ADR-0012 rework. Transitional shape: the two-layer intake gate stays inline in `engine.Dispatch` until ADR-0010 swaps it for registered framework validators. Spike `fsm-architecture.md` remains canonical-pending until all four sub-ADRs land.

**FSM-architecture open-questions walk complete (2026-05-10).** Q-by-Q resolution of the deferred items in [`spikes/fsm-architecture.md`](spikes/archive/fsm-architecture.md). Six structural locks landed (D12–D17 in the spike): session lifecycle as engine field (typed `SessionState`, not Store slice) with B+C hybrid (host orchestrates, engine hooks) flagged; validator pipeline policy (short-circuit / registration order / independent / state via DI; uniform across all `GameCommand` kinds); engine-only effect firing (rolls back 2026-05-08 Q1 cast-handler dispatch); `PendingEffectsSlice` as the universal effect queue (D-universal); effects as async coroutines (`IEffect.Run(scope) : Task`; no state-machine steps; save/load mid-effect not supported); `Request<TResponse>` / `Response` as state-linked service (`RequestsSlice` + `AnswersSlice` + `IRequestService`); single `Dispatch` surface on `IEffectScope` routing all five `GameCommand` siblings. Spike rewritten end-to-end. OPEN-ITEMS audit cascaded (5 closed, 4 added, 5 updated, 1 demoted to task — net -1 entry). GLOSSARY updated (21 additions; 9 retirements). **The big structural questions in `Engine + runtime` are settled** — remaining entries are deferred-with-trigger, small follow-ups, tasks, or authoring concerns. **Vertical-slice-ready.**

**Engine reorg Phase A landed (2026-05-09).** Six-item audit list opened; first four (engine-side) cascaded as one move:

- **Split `engine/02-gamerules-contract.md`** into `02-game-rules-definition.md` (data half — trimmed to ~110 lines per user feedback that the Definition class is too thin for a fat doc) + `03-game-rules-runtime.md` (runtime brain, ~444 lines, lifts most of the original).
- **Renamed + renumbered `engine/03-snapshot-pipeline.md` → `04-available-actions.md`** (H1 changed from "Snapshot Production" to "Available Actions"; framing now around the `IAvailableActions` surface).
- **Renumbered `engine/07-game-definition.md` → `06-game-definition.md`** (clean engine sequence: 01–06).
- **`ISliceProvider` audit deferred to Phase B** — the entities split (next) is the natural moment to decide whether `ISliceProvider` lives in entities or stays inline in `06-game-definition`.

Cascade: 17 files updated for cross-refs (FEATURE-MAP model breakdown + dependency graph cycle observation; GLOSSARY GameRulesDefinition/Runtime entries; OPEN-ITEMS three engine references; engine.md feature root architecture section + execution arc + framework-slice catalog row; engine/01, engine/05, actions/01, actions/03, actions/04, actions/05, effects/effects.md, effects/03, presentation/01, players/01, triggers/triggers.md, triggers/01, pile-counter sample, ADRs 0006/0007 context-doc fields).

**Phase C landed (2026-05-09) — triggers split per type axis.** Audit pass after Phases A+B flagged `triggers/01-trigger-fold.md` H1 ("Trigger And Reaction System") + filename ("trigger-fold") as failing three Model gates: cardinality (multi-type bundle "and"), title-filename equality, and process-shaped filename. Following `_docs/model.md` rigorously: split into `01-trigger.md` (`Trigger<T>` reaction class + `ITriggerSystem` registration + `AbilityDefinition.Register` lifecycle; Q2 provenance) + `02-trigger-pipeline.md` (`ITriggerPipeline` orchestrator + `TriggerFoldResult` + `TriggerMatch` + fold mechanic + three before-fold outcomes + replacement-of-replacements + priority/ordering + `FifoPipeline` default; Q1 + Q3 provenance). Cross-Model synthesis (game-style scheduling table for Slay the Spire / Hearthstone / MTG, history-when-replaced narrative, dispatch-channel discussion, signal/composite emit guidance, pipeline integration) absorbed into `triggers.md` feature root. Cascade: 14 files with cross-refs updated; FEATURE-MAP triggers row 1→2 Models. Audit list now strictly conformant.

**Phase B landed (2026-05-09).** Split `entities/01-definition-instance-model.md` (327 lines) + `entities/02-runtime-surface.md` (723 lines) into five concept Models:

- **`01-definition.md`** (184 lines) — `IEntityDefinition` / `EntityDefinition` / `DefinitionWithSlices`; **canonical home for `ISliceProvider`** (resolves item 4 of the original audit list); Q1, Q2, Q5 provenance.
- **`02-instance.md`** (250 lines) — `InstanceWithSlices<TDef>` handle; `Ref<T>` identity; zone-movement default; lifecycle (spawn / resolve / destroy / snapshot); slice-level subscriptions; Q3, Q4 provenance.
- **`03-variable.md`** (206 lines) — `Variable` / `VariableValue` authoring; `EntityState.BaseValues`; per-variable read/mutation API; `AddEntityVariablePayload` + `SetBaseValuePayload` disciplines.
- **`04-modifier.md`** (209 lines) — `VariableModifier<T>` / `ActiveModifier`; `ModifierSource`; `IModifierFactory` (deterministic ids); `EntityState.ModifierStacks`; modifier mutation API.
- **`05-attachment.md`** (295 lines) — attachments/equipment/tokens/transformation; ongoing effects as attachment entities; cleanup machinery; worked equip/unequip + apply-aura examples; Q6 provenance.

Cascade: front-matter `references:` arrays updated across cards, zones, players, effects, actions, engine; entities feature root rewritten (architecture + composition table now five rows); FEATURE-MAP entities row (1 → 5 Models) + model breakdown rewritten; OPEN-ITEMS section anchors refreshed.

**Earlier this same week (2026-05-08).** Effect-pipeline spike re-grounded around graphflow — 10 decisions locked via design pass. Walked Q1–Q10 in design-pass mode. **Spike re-write pending** — locked decisions not yet applied to [`spikes/effect-pipeline.md`](spikes/effect-pipeline.md); models stay frozen until the spike re-write lands.

Locked shape (from the design pass — not yet patched into the spike):

- **Unifying dispatch (Q1).** `EffectInstance(Source, EffectId, Payload)` is produced by both cast handlers and the trigger fold; both route through `IEffectPipeline.RunEffect`. Trigger-as-EffectInstance gap (#6) dissolves — there is no special "wrap-as-instance" mechanism.
- **Pure split (Q2).** `EffectInstance` is state form; `IEffectScope` is per-execution context; `Flow` stays graphflow-internal and never appears in spike types. Hand-written effects are first-class; graphflow is *one* consumer of the contract via an adapter at the integration boundary.
- **Off-state subscription registry (Q3).** `EffectSubscriptionRegistry` as engine plumbing (not a slice); zone-keyed; built at match start; observer-driven via `ZoneContentsState`. Non-card triggers register in a global bucket with `Source = WorldRef`-style synthetic actor.
- **Off-state definition catalog (Q5).** `IEffectDefinitionCatalog` as sibling plumbing; resolves `(Source, EffectId) → EffectDefinition` for both card-bound and game-bound effects. D8's two verbs unchanged.
- **Chain on `IEffectScope` (Q6).** `Chain.Origin` + `Chain.Fired`; tracing + re-entrance.
- **Authoring deferred (Q7, Q8).** Match-build integrity and `Trigger<T>` syntax sugar both defer to authoring feature. Spike defines runtime mechanics only; samples re-cast in pure `EffectDefinition` form.
- **Decisions dispatched (Q9).** `IEffectScope.Decide` dropped; decisions are `GameAction`s through the full pipeline; handlers internally use `ICommandAwaiter` (DI).
- **D11/D12 unchanged (Q10).** Envelope-as-atomicity and validators-off-handler survive. New open-item: decision-action prompt emission rolls into `oq-fsm-game-state`.

Earlier this same day (2026-05-08): **template-migration wave landed.** All nine features (`cards`, `zones`, `triggers`, `actions`, `presentation`, `players`, `entities`, `effects`, `engine`) now follow the post-simplification feature/Model template. 21 ADRs handled across the wave: 2 elevated to project-wide (`decisions/0007-five-validation-layers.md`, `decisions/0008-zone-visibility-model.md`; ADR-0006 was already project-wide), the rest folded into Model provenance below the `---` divider. Migration landed via PRs #107 (cards), #108 (zones), #109 (triggers + ADR-0006→0008 ref fix), #110 (actions + ADR-0007 elevation), #111 (presentation), #112 (players), #113 (entities), and #114 (effects + engine + sweep). Lint sweep (#116) followed: bare relative `models/...` links across the nine feature index pages and `Product/decisions/...` `references:` entries with the leading `Product/` prefix on six Models — both fixed in place; ADR-0003's `Product/ledgers/` listing deduplicated.

Earlier in the broader project: triggers + dispatch design pass + runtime-contract reshape — closed and **patch cascade landed** ([PR #104](https://github.com/MgCohen/Card-Framework/pull/104)) introducing ADR-0006 (two-pipeline architecture), the runtime contract reshape (try-get `Dispatch`, two-method runtime, deletions of `Route` / `IIntakeQueue` / etc.), and the three-level model (commands / effects / triggers ⊆ effects). Effect-pipeline working sketch lives at [`spikes/effect-pipeline.md`](spikes/effect-pipeline.md) — still pre-canonical.

Two parallel cascades landed earlier (2026-05-06) via [PR #102](https://github.com/MgCohen/Card-Framework/pull/102):

**Open-seams design pass + patch cascade.** 10 structural decisions surfaced when drawing the domain model out and propagated into the canonical Models:

- `entities/01-definition.md` — §"Definitions" reframed around the `DefinitionWithSlices` parent class; §"Store And Entities Relationship" Card/Zone bullets updated to `InstanceWithSlices` + unsealed.
- `entities/02-instance.md` — §1 reshaped: `InstanceWithSlices<TDef> : Scaffold.EntityInstance<TDef>, ISliceProvider` parent class, `Definition` is `protected`, subclass-re-export discipline documented.
- `cards/01-card-model.md` — `Card` unsealed; `CardRegistry` removed; new "Resolution surfaces" section pinning the two-hop path.
- `zones/01-zone-model.md` — `Zone` unsealed; visibility policy replaced with the sealed-record `ZoneVisibilityPolicy` hierarchy; Read API split between `GetCardRefs(state)` and `GetCards(store)`.
- `players/01-player-model.md` + `players/players.md` — `Player` unsealed; `PlayerDefinition` re-parented onto `DefinitionWithSlices`.
- `engine/05-game-factory.md` — added "Registration contract" section.
- `engine/01-game-engine.md` — added §4.4 "Match setup".
- `engine/06-game-definition.md` — `ISliceProvider` reach-points reframed.
- ADRs: `cards/decisions/0001` and `zones/decisions/0001` — open-seams amendments at the top + sample code updated.
- `ledgers/GLOSSARY.md` — `CardRegistry` retired; `DefinitionWithSlices`, `InstanceWithSlices<TDef>`, `ZoneVisibilityPolicy` added; `Card`/`Zone`/`Player` entries updated.
- `ledgers/OPEN-ITEMS.md` — `bl-zone-visibility-custom-policy-serialization` (Q7), `bl-card-resolution-helper-shape` (Q6), `bl-pile-refs-index` (PileCounter follow-up) added.

**PileCounter sample refresh + cascade tail cleanup.** `support/samples/pile-counter.md §9` fully rewritten — refs throughout, per-zone `PileState`, framework `MoveCardMutation` for the zone move with game-side payloads (`AdjustPileCounterPayload`, `SetLastMovePayload`) for counter cache + last-move memory, `PileCounterGameDefinition` bundle, runtime-driven setup commands. The four Models that referenced its outdated patterns (`engine/03`, `actions/02`, `presentation/01`, `engine/04`) refreshed to match. `effects/04` mutator example updated.

Earlier in the broader project: domain modeling design pass + patch cascade **both landed** (2026-05-06). 13 decisions propagated into canonical Models (`engine/06-game-definition`, `players/01-player-model`, `engine/05-game-factory` MatchSetup reshape, `ISliceProvider` adoption across cards/zones/entities, ADR-0005 + glossary + FEATURE-MAP updates, domain-model spike superseded). Plan A/B/C doc cascade landed earlier ([PR #100](https://github.com/MgCohen/Card-Framework/pull/100)).

---

## 🎯 Active focus

**Pile-counter vertical-slice prep.** FSM-arch cascade + consolidation are complete (4 of 4 sub-ADRs landed + spike archived + ledgers swept). Next thread is rewriting [`PLANS/pile-counter-vertical-slice.md`](../PLANS/pile-counter-vertical-slice.md) — currently uses retired vocabulary (`IHandlerContext`, `ActionTemplate`, global `IIntentMapper`, `IAvailableActions` service, `ICommandAwaiter`, `EngineLifecycle` enum) and needs a vocabulary refresh against the landed ADRs before execution begins. The high-level plan shape (states, handlers, mutators, sample game) should survive intact.

Cascade summary (for reference):

- ✅ **ADR-0009 — Engine as FSM** (landed 2026-05-10). D1–D5 + D12. Engine Models 01 / 03 patched; 04 archived; new Models 07 / 08 / 09 added.
- ✅ **ADR-0010 — Validator / handler split** (landed 2026-05-11). D10–D11. Actions row 5→6 (new `06-command-validator`; heavy patch to `03-command-handler`). Effects row drops `03-handler-context` (archived). Engine Models 01 + 03 + 07 patched. ADR-0007 amendment added.
- ✅ **ADR-0011 — Effect orchestration + dispatch + requests** (landed 2026-05-11). D13–D17. Effects row stays at 4 (01 + 02 rewritten; 04 archived; new 05 + 06). Engine Models 01 + 03 patched (drain Phase 2; `Evaluate` drain-policy guards). Actions/01 patched (5-sibling family). Spikes/effect-pipeline.md archived.
- ✅ **ADR-0012 — Presentation per-state ViewModels** (landed 2026-05-11). D6–D9. Presentation row 1→2 (new `01-match-state-view-model` + `02-view-model-registry`; old `01-presentation-loop` archived). Actions row drops `02-intent-mapper` + `05-available-actions-snapshot` (both archived). Feature root rewritten.
- ✅ **FSM-arch consolidation** (landed 2026-05-11). Items 1–4 of the post-cascade plan: spike archived, GLOSSARY + OPEN-ITEMS swept, FEATURE-MAP cycle observation rewritten, ADR + Model path refs repointed.

Deferred follow-ups (separate PRs, in order):

- **`general/ARCHITECTURE.md` rewrite** — current content is research-phase; needs promotion to "current locked architecture" anchored by ADRs 0005–0012 + the 10 settled features. Real authoring lift (~500 lines of pre-cascade content).
- **`support/samples/pile-counter.md` rewrite** — currently carries a "post-cascade rewrite pending" banner. Sibling task to the PLANS revision below.
- **PLANS/pile-counter-vertical-slice.md vocabulary refresh** — repoint to landed ADRs (see Active focus above).
- **Stale `[ADR-NNNN — *forthcoming*]` markers** — ~10 occurrences across `features/engine/engine.md`, `features/engine/models/01-game-engine.md`, `features/presentation/models/01-match-state-view-model.md`, `features/effects/models/06-request-system.md`, `features/actions/models/03-command-handler.md`, `features/actions/models/06-command-validator.md`. Predate the consolidation; each cascade PR left them in place. Mechanical sweep.

---

## ❓ Open items snapshot

See [`ledgers/OPEN-ITEMS.md`](ledgers/OPEN-ITEMS.md) for the full list. After the 2026-05-10 cascade: **no remaining big-structural opens** in `Engine + runtime`. What's left is deferred-with-trigger, small follow-ups, tasks, or authoring-feature work. The three highest-leverage remaining (all task-shaped):

- `oq-effect-pipeline` (now task) — rewrite the effect-pipeline spike to inherit FSM-arch locks.
- `bl-interaction-feature` / `bl-authoring-feature` — scaffold the two missing features. Authoring is the larger lift.
- `oq-effect-cancellation-on-match-end` (now task) — thread `CancellationToken` through `IEffectScope.Dispatch`; engine GCs request/answer slices on match-end.

New today (2026-05-10): `oq-targeting-pre-play`, `oq-validator-defaults`, `oq-effect-save-load`, `oq-drain-policy-composition` — all defer-tier or small.

Closed today: `oq-runtime-as-state-evaluator`, `oq-fsm-game-state`, `oq-session-state-machine`, `oq-intent-template-lookup`, `oq-move-validation-execution-drift`.

---

## 📝 Scratch (ephemerals)

*Mid-conversation questions worth holding onto for the current session, but not yet promoted to OPEN-ITEMS. End of session: answer (delete), promote (move to OPEN-ITEMS), or leave for next session.*

*Scratch is empty — domain-modeling thread closed; locked decisions landed in the canonical Models.*

---

## How to use this file

- **Update continuously as work happens** — agent keeps this current; not refreshed only at session end.
- **Keep it short** — if it grows past two screens, trim. Detail belongs in feature docs and OPEN-ITEMS entries.
- **`## Scratch`** is for ephemerals — things being chewed on right now. Promote to OPEN-ITEMS if they survive a session.
- **No artifact created at session end.** `/builder:snapshot` makes an opt-in milestone copy in `sessions/` when the user asks.
