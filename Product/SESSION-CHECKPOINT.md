---
type: checkpoint
tags: [#checkpoint]
---

# Session Checkpoint

**Live spine.** This is the single source of truth for "where we are." Updated continuously as work happens. No open/close lifecycle.

---

## Where we stopped

Design pass on `oq-game-definition` opened and suspended after Q1 — **Q1 locked**: `GameDefinition` bundle owns slice schema + initial-value instantiation. Mechanism is `ISliceProvider<TInstance>` (per-spawn-keyed; entity and zone definitions implement) + unparameterized `GameDefinition.GetInitialSlices()` for singletons; factory enumerates `(definition, instance)` pairs at `Build` and installs declared slices. Manual `WithInitialSlices(...)` overlay survives as test/script escape hatch. Cardinality lives in the spawn list, not the interface. Stages 2 (per-kind domain modeling: Card, Zone, Player, Game) and 3 (zone layout, `GameDefinition` shape, `MatchSetup` reshape, asset composition) deferred to a dedicated full-domain-modeling session before resuming. No doc patches land until the design pass completes.

Prior pass on `oq-engine-match-lifecycle` landed — new `engine/06-match-lifecycle.md` written; cascade applied across `engine/01` (Dispatch lifecycle gate), `engine/02` (session-lifecycle framing flipped, `EndGameCommand` → `EndMatchCommand`, lifecycle open item moved to Resolved), `engine/03` (sample uses `EndMatchCommand` + `MatchOutcome.Win`), `engine/04` (`EngineState.Lifecycle` field), `engine/05` (return state, scope-ownership softened, Inv-9 narrowed, open follow-ups cleaned), pile-counter (`EndMatchCommand` rename, runtime takes `actors` ctor param, Q3 struck), glossary (5 new entries: `EngineLifecycle`, `MatchOutcome`, `StartMatchCommand`, `EndMatchCommand`, `MatchStartedSignal`, `MatchEndedSignal`), OPEN-ITEMS (`oq-engine-match-lifecycle` struck; `oq-effect-cancellation-on-match-end` added). Prior `oq-gamerules-runtime-definition` cascade also fully landed.

---

## 🎯 Active focus

Full domain modeling pass — sketch every slice / state / aggregate the framework needs across cards, zones, players, mappings, from currently-locked decisions. Out of scope: commands, handlers, mutators, services, runtime composition (those are already designed). Output: a domain model that becomes the input to Stages 2 (per-kind `ISliceProvider` implementations) and 3 (zone layout, `GameDefinition` shape, `MatchSetup` reshape, asset composition) of the suspended `oq-game-definition` design pass. Open in a fresh session.

---

## ❓ Open items snapshot

See [`ledgers/OPEN-ITEMS.md`](ledgers/OPEN-ITEMS.md) for the full list. The three highest-leverage durable items right now:

- `oq-game-definition` — `GameDefinition` bundle + definition-declared slice schema. Load-bearing for asset-driven match construction.
- `oq-entity-destruction` — Cleanup lifecycle for destroyed entities. Load-bearing for ongoing effects that self-destruct.
- `oq-effect-cancellation-on-match-end` — async-effect cancellation when the match ends mid-await. Surfaced from the lifecycle pass.

---

## 📝 Scratch (ephemerals)

*Mid-conversation questions worth holding onto for the current session, but not yet promoted to OPEN-ITEMS. End of session: answer (delete), promote (move to OPEN-ITEMS), or leave for next session.*

### Pending thread — drop the entities bridge?

Mid-conversation, the `EntityDefinition`-as-base question surfaced a deeper one: are the `com.scaffold.entities` + `com.scaffold.entities.states` packages adding value commensurate with their cost given the framework's redux-shaped direction? Sketches captured in [`spikes/entities-bridge-replacement.md`](spikes/entities-bridge-replacement.md) — **draft / exploratory, not approved or denied**. User stepped back to think before deciding. If this lands, it's upstream of the domain modeling pass — `CardDefinition`'s base, what "card slices" means, and the Definition hierarchy all reshape. Resume here next session: either decide on the spike (open as `oq-drop-entities-bridge` design pass) or punt and proceed with the domain modeling pass against the current bridge.

### Kick-start brief — domain modeling session

- **Goal.** Sketch every slice / state / aggregate the framework needs across cards, zones, players, mappings, before resuming `oq-game-definition` Stages 2–3.
- **Inputs.** Q1 lock on `ISliceProvider<TInstance>` (informs what each definition kind will eventually declare); `engine/04` slice catalog (framework vs game-authored tiers); `cards/01`, `zones/01`, `entities/01` (current state surfaces); glossary.
- **Out of scope.** Commands, handlers, mutators, services, runtime composition.
- **Output.** A domain model enumerating every slice, keyed by which definition kind owns it (card-keyed, zone-keyed, actor/player-keyed, game-singleton). Should reveal whether aggregates are needed, whether new states surface, and whether per-actor / per-player slice patterns exist beyond the current `Player : Actor` baseline.
- **Why first.** Once we know what each definition kind owns, the per-kind `ISliceProvider` implementations (Stage 2) become mechanical, and `GameDefinition` shape + asset composition (Stage 3) can be designed against a concrete slice surface rather than a hypothetical one.
- **Dependency.** Resolve the entities-bridge thread (above) first if pursuing — it changes the slice surface every definition kind would declare.

---

## How to use this file

- **Update continuously as work happens** — agent keeps this current; not refreshed only at session end.
- **Keep it short** — if it grows past two screens, trim. Detail belongs in feature docs and OPEN-ITEMS entries.
- **`## Scratch`** is for ephemerals — things being chewed on right now. Promote to OPEN-ITEMS if they survive a session.
- **No artifact created at session end.** `/builder:snapshot` makes an opt-in milestone copy in `sessions/` when the user asks.
