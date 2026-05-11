---
type: checkpoint
tags: [#checkpoint]
---

# Session Checkpoint

**Live spine.** This is the single source of truth for "where we are." Updated continuously as work happens. No open/close lifecycle.

---

## Where we stopped

**ADR-0010 — Validator / handler split landed (2026-05-11).** Second sub-ADR of the FSM-arch cascade (sub-ADR B). [`decisions/0010-validator-handler-split.md`](decisions/0010-validator-handler-split.md) ratifies spike D10–D11: `ICommandValidator<TCommand>` splits from `ICommandHandler<TCommand, TResult>`; `GameCommandHandler<,>` and `CommandValidator<>` abstract bases land with `[Inject] protected Store`; `ValidatedCommandHandler<,>` combined base covers the common case; validators are uniform across every `GameCommand` subtype; short-circuit on first reject, DI registration order = run order; `IHandlerContext` retired (archived); ADR-0009's inline two-layer intake gate converts to two registered framework-default validators `SessionGate` + `MatchStateActionGate`. Cascade: new Model `actions/06-command-validator.md` (`ICommandValidator<TCommand>` canonical home; the `SessionGate` and `MatchStateActionGate` framework defaults); heavy patch to `actions/03-command-handler.md` (rewrites around `GameCommandHandler<,>` base; removes the `Validate` member and the `IHandlerContext` parameter); archive of `effects/03-handler-context.md` to `effects/archive/`; patches to `actions/01-command-pipeline.md` (validator chain replaces inline gate + `handler.Validate`), `actions/02-intent-mapper.md` (wording refresh + rework-pending banner for ADR-0012), `effects/02-effect-scope.md` (front-matter ref to retired `effects/03` swapped), `effects/04-command-awaiter.md` (rework-pending banner for ADR-0011), `engine/01-game-engine.md` (inline gate block deleted; §Intake gates rewritten as the validator chain; loop semantics, Pile Counter example, and Open follow-ups refreshed), `engine/03-game-rules-runtime.md` (Position-in-the-system table splits Validate from Handle; Five rejection paths table refresh; Anti-scope entries point at the new homes), `engine/07-match-state.md` (transition-handler example rewritten to `GameCommandHandler<,>` base; intake-gate note refreshed), `entities/05-attachment.md` (three handler code samples updated to `GameCommandHandler<,>` shape; front-matter ref swapped), feature roots `actions.md` (composition table 5→6; vocabulary table absorbs the new entries; five-validation-layers table refreshed) + `effects.md` (composition table drops handler-context row; architecture text reflects retirement; three-layers-capabilities table refreshed); amendment block added to `ADR-0007` mapping the five-validation-layer model to the post-ADR-0010 expression; FEATURE-MAP actions row 5→6, effects row 4→3, reorg note appended; GLOSSARY framework-default validators (`SessionGate`, `MatchStateActionGate`) added, `EngineLifecycle` retired-term entry refreshed, `IHandlerContext` retired-term entry refreshed; OPEN-ITEMS `oq-validator-defaults` refreshed (the two canonical defaults are now defined; remaining opens are ordering-enforcement and a possible third default).

**ADR-0009 — Engine as FSM landed (2026-05-10).** First sub-ADR of the FSM-arch cascade per the agreed decomposition (4 sub-ADRs by axis: A engine-as-FSM, B validator/handler split, C effect orchestration + dispatch + requests, D presentation per-state VMs). [`decisions/0009-engine-as-fsm.md`](decisions/0009-engine-as-fsm.md) ratifies spike D1–D5 + D12: engine IS the FSM (`Evaluate → MatchState`); `MatchStateSlice` wraps polymorphic `MatchState`; states are data + `ShouldExit`; successor knowledge in `ITurnFlow.NextAfter`; cross-cutting overrides via `ISBA`; session lifecycle is typed sealed `SessionState` on `engine.Session` field. Cascade: three new Models (`engine/07-match-state`, `engine/08-turn-flow`, `engine/09-state-based-actions`); heavy patches to `engine/01-game-engine` (replaced `EngineLifecycle` enum with `SessionState` typed hierarchy; intake gate now two-layer — `Session` + `MatchState.Actions`; available-actions snapshot section removed; `EngineState.Lifecycle` field retired) and `engine/03-game-rules-runtime` (`Evaluate(action, result) → MatchState`; `Flow` + `StateBasedActions` abstract properties; default `Evaluate` body composes SBAs + ShouldExit + Flow; `IAvailableActions` removed from injected services; example Pile Counter runtime updated); `engine/04-available-actions` archived to `engine/archive/`; engine feature root rewritten (Composition 6→8, framework-slice catalog swaps `AvailableActionsState` for `MatchStateSlice`); FEATURE-MAP engine row 6→8 with archive note; cross-feature refs in `actions/05`, `presentation/01` get banners flagging upcoming ADR-0012 rework. Transitional shape: the two-layer intake gate stays inline in `engine.Dispatch` until ADR-0010 swaps it for registered framework validators. Spike `fsm-architecture.md` remains canonical-pending until all four sub-ADRs land.

**FSM-architecture open-questions walk complete (2026-05-10).** Q-by-Q resolution of the deferred items in [`spikes/fsm-architecture.md`](spikes/fsm-architecture.md). Six structural locks landed (D12–D17 in the spike): session lifecycle as engine field (typed `SessionState`, not Store slice) with B+C hybrid (host orchestrates, engine hooks) flagged; validator pipeline policy (short-circuit / registration order / independent / state via DI; uniform across all `GameCommand` kinds); engine-only effect firing (rolls back 2026-05-08 Q1 cast-handler dispatch); `PendingEffectsSlice` as the universal effect queue (D-universal); effects as async coroutines (`IEffect.Run(scope) : Task`; no state-machine steps; save/load mid-effect not supported); `Request<TResponse>` / `Response` as state-linked service (`RequestsSlice` + `AnswersSlice` + `IRequestService`); single `Dispatch` surface on `IEffectScope` routing all five `GameCommand` siblings. Spike rewritten end-to-end. OPEN-ITEMS audit cascaded (5 closed, 4 added, 5 updated, 1 demoted to task — net -1 entry). GLOSSARY updated (21 additions; 9 retirements). **The big structural questions in `Engine + runtime` are settled** — remaining entries are deferred-with-trigger, small follow-ups, tasks, or authoring concerns. **Vertical-slice-ready.**

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

**FSM-arch cascade in progress (2 of 4 sub-ADRs landed).** The structural design is settled across FSM, dispatch, effects, requests, lifecycle layering, and validators. Decomposed into 4 sub-ADRs by axis:

- ✅ **ADR-0009 — Engine as FSM** (landed 2026-05-10). D1–D5 + D12. Engine Models 01 / 03 patched; 04 archived; new Models 07 / 08 / 09 added.
- ✅ **ADR-0010 — Validator / handler split** (landed 2026-05-11). D10–D11. Actions row 5→6 (new `06-command-validator`; heavy patch to `03-command-handler`). Effects row 4→3 (`03-handler-context` archived; `04-command-awaiter` rework-pending under ADR-0011). Engine Models 01 + 03 + 07 patched (intake gate retires; rejection-paths table refreshed; transition-handler example updated). ADR-0007 amendment added mapping the five-validation-layer model to the new shape.
- ⏳ **ADR-0011 — Effect orchestration + dispatch + requests.** D13–D17. Patches: `effects/01`, `effects/02`; retires `effects/04`. Adds `actions/01` `GameCommand` family expansion. New Models: pending-effects, request-system. Structurally independent of 0009 and 0010.
- ⏳ **ADR-0012 — Presentation per-state ViewModels.** D6–D9. Patches: `presentation/01`, `actions/02`, `actions/05`. New Model: viewmodel-registry. Depends on ADR-0009 (per-state VMs reference `MatchState`).

Other open threads:

- **Effect-pipeline spike rewrite** — inherit FSM-arch locks; folds into ADR-0011 cascade. Tracked as task-shaped `oq-effect-pipeline` in OPEN-ITEMS.
- **First vertical-slice prototype** — build `PileCounterGameDefinition` against the settled domain model. Can start in parallel; touchpoints with the cascade are localized to the two remaining affected axes.

Next natural thread: ADR-0011 (independent of 0010; effect-pipeline spike rewrite folds into the same PR; structurally independent of ADR-0009).

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
