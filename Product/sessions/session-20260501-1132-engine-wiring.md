---
type: session
schema-version: 2
session-id: session-20260501-1132-engine-wiring
parent: session-20260501-1107-zone-topology
status: completed
started-at: 2026-05-01T11:32:00Z
last-updated: 2026-05-01
work-summary: "Design pass (6 Qs) + patches applied: IHandlerRegistry resolver-only, ICommandPipeline owns validate/execute, ITriggerSystem/ITriggerPipeline split, drain loop bug fixed, VContainer wiring, GameEngineBuilder as config object."
tags: [#session]
---

# session-20260501-1132-engine-wiring

## Part 1 — Active question coming in

**Engine wiring stack** — `IHandlerRegistry` interface, `GameEngineBuilder` construction protocol, `ICommandPipeline` interface, engine startup / first `OnQueueDrained` trigger, `GameRules` service wiring. Cluster of 5 — one design-pass session.
*(From session-20260501-1107-zone-topology.)*

---

## Part 2 — Work done

- Design pass: 6 Qs on engine wiring stack — IHandlerRegistry shape, GameRules services, ITriggerSystem/ITriggerPipeline split, drain loop fix, VContainer wiring, GameEngineBuilder config object.
- Patches applied to `engine/01`, `engine/02`, `triggers/01`, `pile-counter` — all Q1-Q6 decisions landed.
- Updated `/builder:design-pass` skill: structured context summary, one-liner question list, no confirm-order gate.
- debt: 4 opened (ad-20260501-022 through 025)

---

## Part 3 — Decisions settled

- Q1 = `IHandlerRegistry` is a resolver only — returns non-generic `ICommandHandler` base; `Validate` and `Execute` move to `ICommandPipeline`; game authors extend `CommandHandler<TCommand, TResult>` which bridges down
- Q2 = No pre-validation service on the rule — `pipeline.Validate` runs at intake, rule's `OnIncomingCommand` receives an already-validated command and defaults to accepting; rule constructor loses `IHandlerRegistry`
- Q3 = `ITriggerSystem` (subscription, card abilities) and `ITriggerPipeline` (execution, pipeline-internal) are two separate interfaces on one concrete `TriggerSystem` class
- Q4 = Keep current drain loop; fix the bug: move `running = false` before `OnQueueDrained` dispatch so re-entry works; no forced drain at startup — engine starts dormant, first `Dispatch()` triggers it
- Q5 = VContainer for service wiring; framework ships an `EngineInstaller`; game authors write their own installers; `GameRules` uses plain constructor injection
- Q6 = `GameEngineBuilder` kept as a match configuration object (handlers, mutators, rules, slices, players, seed); receives the VContainer at `Build(container)` for framework service resolution; leaves a clean path to asset-driven configuration

---

## Part 4 — Active question going out

Discharge the 4 debt entries opened this session (ad-20260501-022 through 025):

1. **ad-022**: Update `engine/03-snapshot-pipeline.md` — GameRules constructor to 3 services, remove `IHandlerRegistry Handlers`
2. **ad-023**: Update `engine/decisions/0001-gamerules-as-class-with-services.md` — remove `IHandlerRegistry` from service list
3. **ad-024**: Update `OPEN-QUESTIONS.md` — strike through oq-handler-registry, oq-command-pipeline, oq-engine-startup, oq-gamerules-service-wiring, oq-trigger-type-unification
4. **ad-025**: Reconcile `ITriggerPipeline` shape between `effects/02 §3` (authoritative, has `TriggerMatch`) and `triggers/01 §7` (added this session, simpler) — pick one authoritative definition

---

## Part 5 — Debt opened / closed

- ad-20260501-022 opened: engine/03-snapshot-pipeline stale IHandlerRegistry
- ad-20260501-023 opened: ADR-0001 stale IHandlerRegistry service list
- ad-20260501-024 opened: OPEN-QUESTIONS.md — 5 oq-IDs to strike through
- ad-20260501-025 opened: ITriggerPipeline shape conflict between effects/02 and triggers/01
