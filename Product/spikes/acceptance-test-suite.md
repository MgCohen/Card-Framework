# Acceptance Test Suite — Spec-Driven Subcutaneous Tests

> Natural-language **pictures** are the canonical spec; an LLM is the
> **author-time** generator; deterministic **NUnit** tests are the run-time gate.
> Each test drives the **real engine** through the composition root, enters at the
> **client-intent seam below the UI**, and asserts the **server→client loop**
> converges — the way a real player exercises the game, minus the UI.
>
> Status: planning (2026-06-04). Builds on the command-service spike (Phase 0–2 shipped).

## Why this exists

Today's tests are good but narrow:

- **Unit** per feature (Tags, Entities, Requests, States, Commands) — component contracts.
- **Two integration spines** — `EngineStack` (hand-wired) and `BootstrapWiring`
  (real DI composition root). Both prove the layers compose.

The gap: neither drives the system **the way a user does**. They (a) run
**server-only** — one stream, one store-set, `NullTransport` — so the substrate's
core promise (*same handler both sides → converge*) is untested for commands; and
(b) enter at `engine.Commands.Execute(...)`, a server-internal call **below the
user's boundary**. A real player submits an *intent* from a client; the server
decides; mutations cross the wire; both peers converge; reactions fire.

This suite closes that gap with **Spec-Driven Development (SDD)** — the post-BDD,
AI-era shape. We keep BDD's one good idea (behavior described in domain language as
executable living documentation) and drop everything the LLM makes obsolete
(Gherkin's rigid syntax, the step-definition toolchain). Industry consensus is
explicit and decisive here: a **deterministic** system (ours is event-sourced and
convergent — it has exact oracles like `health == 18`) is validated by **code
assertions**, *not* an LLM judge. LLM-as-judge / LMUnit-style validation is for
**non-deterministic output** (grading generated text), which we do not have.

## Locks (non-negotiable)

- **L1 — Pictures are canonical.** Free-prose markdown (Goal / Given / When / Then /
  Invariants). No Gherkin rigidity, no exact-match steps, no step-definitions.
- **L2 — AI at author-time, determinism at run-time.** The LLM *writes* and *audits*
  tests; it never decides pass/fail on a run. The committed NUnit test is the gate.
- **L3 — Through the real composition root.** Boot via the actual `Bootstrap` layers
  ("as if Play was pressed"). Never hand-wire what the layers provide. (Hand-wiring
  stays in unit / `EngineStack`; acceptance is the DI `(b)` style, scaled up.)
- **L4 — Enter below the UI.** Drive at the client-intent seam (`Submit`), never
  `CommandService.Execute` directly — that call is below the user's boundary.
- **L5 — Two real sides.** Assert server↔client convergence across **separate
  store-sets** fed by a transport. A one-sided "loop" is not the loop.
- **L6 — One picture ↔ one test, backlinked, deterministic oracle.** Exact expected
  state. No toolchain dependency (no Cucumber / SpecFlow / Reqnroll).

## Artifact layout (provisional — confirmed in Phase A)

```
Assets/Framework/Acceptance/
  Scenarios/                     # the pictures — NL, canonical spec
    _how-to-bind.md              # generator contract (the prompt, written once)
    play-strike-deals-damage.md
  Tests/                         # generated deterministic NUnit bindings
    PlayStrikeDealsDamage.cs
  Harness/                       # MatchHarness, transport, peer plumbing
  Framework.Acceptance.Tests.asmdef
```

Pictures live **with** the test assembly (versioned next to their bindings,
discoverable), not in `Product/`. The generator contract (`_how-to-bind.md`) is the
prose analog of BDD's step-definition library — stated **once**, reused by every
picture.

## Picture template

```
# <Scenario title — the real gameplay moment>

Goal: <one line — what real play this proves>

Given:
- <match started as if Play was pressed; N players; starting state>

When:
- <the single intent a player submits, at the seam below the UI>

Then:
- <events travel the real path; reactions fire; both sides converge; response is X>

Invariants:
- <what must always hold — e.g. illegal intent emits ZERO wire events;
   client never authors state; server/client stores identical once settled>
```

## Phased plan

Each phase lands green (headless batch run) before the next.

| Phase | Ships | Proof | Depends / resolves |
|---|---|---|---|
| **A — Convention + first picture** | The `Scenarios/` format, `_how-to-bind.md` generator contract, the author-time drift-check step. One picture bound to a deterministic test **against today's API** (single-side, interim `Execute` seam) — reuse `Integration.Tests`, no new infra. | One picture ↔ one green NUnit test; the convention round-trips end-to-end. | Settles L1/L2/L6 + artifact layout. |
| **B — Convergence reachability spike** | A decision: is two-sided convergence reachable **headless in EditMode** (a transport ferrying serialized events between two store-sets via AutoPacker/Scaffold) or does it need **PlayMode** (`Wire.PlayTests`/`Multiplayer.PlayTests`)? Minimal two-peer proof. | Peer A emits one mutation → peer B converges; same value both sides. | De-risks L5; picks the suite's run tier. |
| **C — MatchHarness + intent seam** | `MatchHarness.Start(players)` ("as if Play"): real composition root, server + N client peers. The `Submit(player, intent)` **client-intent seam**. `Seed`, `AssertConverged()`. | The Phase A scenario, **rewritten to enter via `Submit`** and assert server↔client convergence — now genuinely subcutaneous. | Resolves command-service deferred *client-submission*; realizes L3/L4/L5. |
| **D — Real gameplay scenarios** | A small high-value picture set: play a damage card; an **illegal play → zero wire traffic**; a **multi-mutation card under one Command block**. Each bound. | N pictures ↔ N green subcutaneous tests through the full loop. | Subsumes command-service **Phase 3** multi-service/ordering proof — now two-sided. |
| **E — Reactions in the loop** | Pictures covering follow-on effects: play → reaction fires → more mutations → both sides converge. (Lands with the reaction era.) | A reaction-driven scenario converges; the reaction fires **exactly once**, not per-side. | Depends on reaction/SBA work (currently deferred). |
| **F — Living-doc hygiene** | Author-time LLM **drift check** ("does this test still satisfy its picture?"), a scenario index, a CI note. | A deliberate picture/test mismatch is flagged by the check. | Keeps L1/L2 honest over time. |

## Relationship to the command-service spike

- **Phase C** resolves its deferred **client-submission** seam (intents enter from a client; the server decides).
- **Phase D** strengthens command-service **Phase 3** ("multi-mutation ordering on the wire from one `Execute`") by making it genuinely **server→client** rather than server-only — the merit-based reorder we agreed: flow-fidelity before server-local atomicity. Multi-service atomicity is proven *inside* a two-sided scenario, not as a separate single-side test.

## Open decisions

- **Picture location** — provisional `Assets/Framework/Acceptance/Scenarios/`
  (with the tests) vs `Product/`. Confirm in Phase A.
- **Convergence tier** — EditMode vs PlayMode. Settled by Phase B's spike.
- **Scenario granularity** — "not too wide": **one intent through the full loop**,
  not a whole match. A scenario is one coherent play.

## Explicitly out of scope / deferred

- **UI projection tests** (humble-object: feed a flow-state to the UI, assert render;
  capture emitted intents). Deferred until a UI exists. Bank the constraint now:
  **the UI holds no logic** — the subcutaneous flow is the total system, the UI is a
  thin projection over it.
- **Run-time LLM-as-judge validation.** Excluded by design. Our engine is
  deterministic with exact oracles; LLM judging belongs to non-deterministic output
  (LMUnit's domain), not here. The only LLM at run scope is *none*; the LLM operates
  at author time (generate + drift-check) only.
