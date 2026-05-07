---
type: checkpoint
tags: [#checkpoint]
---

# Session Checkpoint

**Live spine.** This is the single source of truth for "where we are." Updated continuously as work happens. No open/close lifecycle.

---

## Where we stopped

**Effect-pipeline design pass — iterating in a spike file.** The session settled enough to capture as a working sketch but isn't ready to land in canonical Models. Locked context, decisions, types, services, flow, snippets, and a traced MTG flow live at [`spikes/effect-pipeline.md`](spikes/effect-pipeline.md). Models stay frozen until that sketch settles.

Working shape (from the spike — not yet locked into Models):

- `IEffectPipeline` as injected service, symmetric with `ICommandPipeline`. Framework default = inline (Stacklands); games swap for stack-zone (MTG) / queue-zone (Hearthstone).
- `EffectDefinition` (recipe; lambdas like `Trigger<T>`, subclass optional) + `EffectInstance` (runtime record `(Source, EffectId, Bindings)`).
- State holds `EffectInstance` records (immutable). MTG stack zone, Hearthstone trigger queue = game-authored slices over these records. State authoritative; UI projects.
- Runtime stays at `FilterTriggers + Evaluate`. No `ScheduleEffect` on the runtime.
- Chain scope only created when an effect runs. Drain doesn't construct scopes. `pipeline.Execute(action, chainScope?)` takes optional scope; trigger fold inherits the calling chain. Re-entrance via `Fired` set.
- Two verbs: `IEffectPipeline.RunEffect(instance, parent)` (scheduling decision) + `EffectInstance.Execute(scope)` extension (direct execution; used by resolution handlers to bypass scheduling).
- Triggers compile into effects: `Trigger<T>` is authoring sugar; the fold wraps a matched trigger as `EffectInstance` and calls `RunEffect`.

Earlier this session: triggers + dispatch design pass + runtime-contract reshape — closed and **patch cascade landed** ([PR #104](https://github.com/MgCohen/Card-Framework/pull/104)) introducing ADR-0006 (two-pipeline architecture), the runtime contract reshape (try-get `Dispatch`, two-method runtime, deletions of `Route` / `IIntakeQueue` / etc.), and the three-level model (commands / effects / triggers ⊆ effects).

Earlier in the broader project: domain modeling design pass + patch cascade **both landed** (2026-05-06). 13 decisions propagated into canonical Models (`engine/07-game-definition`, `players/01-player-model`, `engine/05-game-factory` MatchSetup reshape, `ISliceProvider` adoption across cards/zones/entities, ADR-0005 + glossary + FEATURE-MAP updates, domain-model spike superseded). Plan A/B/C doc cascade landed earlier ([PR #100](https://github.com/MgCohen/Card-Framework/pull/100)).

---

## 🎯 Active focus

**Iterating on [`spikes/effect-pipeline.md`](spikes/effect-pipeline.md)** — keep refining the sketch until the shape is settled enough to convert to canonical Models (`effects/04`, `effects/01`, `triggers/01`, `engine/02`, `engine/01`, plus a new ADR-0007 and glossary + open-items updates). Don't update Models from this in-flight design; load it back from the spike when ready.

Open follow-ups surfaced by the sketch (not blocking, listed for later promotion):

- `oq-effect-authoring` — recipe shape inside `EffectDefinition.Execute` (data, code, graph, hybrid). Authoring feature scope.
- `oq-runtime-as-state-evaluator` — long-term direction; `FilterTriggers` is the next candidate to revisit.
- `oq-session-state-machine` — richer engine lifecycle (Connecting / Started / Running / Reconnecting / Ended / ShowingResults) on top of the current 3-state `EngineLifecycle`.

Alternative threads if iteration on the spike pauses:

- **Resolve top open items.** `oq-entity-destruction`, `oq-effect-cancellation-on-match-end`, `oq-attachment-storage`.
- **First vertical-slice prototype.** Build the actual `PileCounterGameDefinition` against the settled domain model + runtime contract.
- **Promote a `considered` feature** (`authoring`, `interaction`, `networking`).

---

## ❓ Open items snapshot

See [`ledgers/OPEN-ITEMS.md`](ledgers/OPEN-ITEMS.md) for the full list. The three highest-leverage durable items right now:

- `oq-entity-destruction` — Cleanup lifecycle for destroyed entities. Load-bearing for ongoing effects that self-destruct.
- `oq-effect-cancellation-on-match-end` — async-effect cancellation when the match ends mid-await. Surfaced from the lifecycle pass.
- `oq-attachment-storage` — where attachments (equipment, auras, buffs) live: sub-zone, global registry, or dedicated zone.

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
