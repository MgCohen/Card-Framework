---
type: checkpoint
tags: [#checkpoint]
---

# Session Checkpoint

**Live spine.** This is the single source of truth for "where we are." Updated continuously as work happens. No open/close lifecycle.

---

## Where we stopped

**Engine reorg Phase A landed (2026-05-09).** Six-item audit list opened; first four (engine-side) cascaded as one move:

- **Split `engine/02-gamerules-contract.md`** into `02-game-rules-definition.md` (data half — trimmed to ~110 lines per user feedback that the Definition class is too thin for a fat doc) + `03-game-rules-runtime.md` (runtime brain, ~444 lines, lifts most of the original).
- **Renamed + renumbered `engine/03-snapshot-pipeline.md` → `04-available-actions.md`** (H1 changed from "Snapshot Production" to "Available Actions"; framing now around the `IAvailableActions` surface).
- **Renumbered `engine/07-game-definition.md` → `06-game-definition.md`** (clean engine sequence: 01–06).
- **`ISliceProvider` audit deferred to Phase B** — the entities split (next) is the natural moment to decide whether `ISliceProvider` lives in entities or stays inline in `06-game-definition`.

Cascade: 17 files updated for cross-refs (FEATURE-MAP model breakdown + dependency graph cycle observation; GLOSSARY GameRulesDefinition/Runtime entries; OPEN-ITEMS three engine references; engine.md feature root architecture section + execution arc + framework-slice catalog row; engine/01, engine/05, actions/01, actions/03, actions/04, actions/05, effects/effects.md, effects/03, presentation/01, players/01, triggers/triggers.md, triggers/01, pile-counter sample, ADRs 0006/0007 context-doc fields).

**Phase B pending** (next): split `entities/01-definition-instance-model.md` and `entities/02-runtime-surface.md` per concept axis (definition / instance / variable / modifier / attachment).

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

- `entities/01-definition-instance-model.md` — §"Definitions" reframed around the `DefinitionWithSlices` parent class; §"Store And Entities Relationship" Card/Zone bullets updated to `InstanceWithSlices` + unsealed.
- `entities/02-runtime-surface.md` — §1 reshaped: `InstanceWithSlices<TDef> : Scaffold.EntityInstance<TDef>, ISliceProvider` parent class, `Definition` is `protected`, subclass-re-export discipline documented.
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

**Phase B of the workflow audit: split `entities/01-definition-instance-model.md` and `entities/02-runtime-surface.md` per concept axis** (definition / instance / variable / modifier / attachment). Each Model gets carved into its concept-aligned successors; cross-refs cascade across cards/zones/players/engine.

Concrete sub-steps (mirrors Phase A's pattern):

- Read both entities Models end-to-end; map content to the five concept Models.
- Decide canonical home for `ISliceProvider` interface declaration (entities-side vs engine/06; surfaced as item 4 from the original audit list).
- Decide canonical home for `DefinitionWithSlices` + `InstanceWithSlices<TDef>` parents (likely the new `definition` and `instance` Models respectively).
- Author the new Models; delete originals; cascade cross-refs.
- Update FEATURE-MAP entities row + model breakdown; update GLOSSARY entries that reference entities Model paths.

Alternative threads if Phase B pauses:

- **Effect-pipeline spike re-write** — apply the locked Q1–Q10 verdicts from 2026-05-08 to [`spikes/effect-pipeline.md`](spikes/effect-pipeline.md). Models stay frozen until this lands.
- **Resolve top open items.** `oq-entity-destruction`, `oq-effect-cancellation-on-match-end`, `oq-attachment-storage`.
- **First vertical-slice prototype.** Build the actual `PileCounterGameDefinition` against the settled domain model + runtime contract.

---

## ❓ Open items snapshot

See [`ledgers/OPEN-ITEMS.md`](ledgers/OPEN-ITEMS.md) for the full list. The three highest-leverage durable items right now:

- `oq-entity-destruction` — Cleanup lifecycle for destroyed entities. Load-bearing for ongoing effects that self-destruct.
- `oq-effect-cancellation-on-match-end` — async-effect cancellation when the match ends mid-await. Surfaced from the lifecycle pass.
- `oq-attachment-storage` — where attachments (equipment, auras, buffs) live: sub-zone, global registry, or dedicated zone.

New today (open-seams + PileCounter follow-ups, deferred to triggers): `bl-zone-visibility-custom-policy-serialization`, `bl-card-resolution-helper-shape`, `bl-pile-refs-index`.

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
