# Acceptance Test Suite — Spec-Driven Subcutaneous Tests

> Natural-language **pictures** are the canonical spec; an LLM is the
> **author-time** generator; deterministic **NUnit** tests are the run-time gate.
> Each test drives the **real engine** through the composition root and asserts an
> **exact oracle** on the resulting state.
>
> Status: planning (2026-06-05). Builds on the command-service spike (Phase 0–2 shipped).

## Build philosophy — baseline first, layers on top

We build this **slowly, one layer at a time**, and validate at each step. The
suite has a clear destination (two real sides, intents entering below the UI,
reactions in the loop) but we do **not** front-load it. The baseline enters at
the **command** (`Execute`) — the layer we are actually building right now — and
runs **single-side**. Everything above the command (the client-submission seam,
two-sided convergence, reactions) is a **later layer**, added deliberately once
the baseline is solid. See "Locks" (now vs deferred) and the deferred ladder.

Concretely, for the baseline we **fake/seed** the starting state directly and
start from the command. We skip the whole upstream chain — player held a card,
dropped it, an intent was created, sent, deserialized, became a command. That
chain is real and we will build it; it is just not this layer.

## Why this exists

Today's tests are good but narrow:

- **Unit** per feature (Tags, Entities, Requests, States, Commands) — component contracts.
- **Two integration spines** — `EngineStack` (hand-wired) and `BootstrapWiring`
  (real DI composition root). Both prove the layers compose.

The gap: neither describes behavior **in domain language** as executable living
documentation, and neither is the seam an author reaches for to add "prove this
game moment works." This suite closes that gap with **Spec-Driven Development
(SDD)** — the post-BDD, AI-era shape. We keep BDD's one good idea (behavior
described in domain language as executable living documentation) and drop
everything the LLM makes obsolete (Gherkin's rigid syntax, the step-definition
toolchain). For a **deterministic** system (ours is event-sourced and convergent
— it has exact oracles like `health == 18`), validation is by **code assertions**,
*not* an LLM judge. LLM-as-judge / LMUnit-style validation is for
**non-deterministic output** (grading generated text), which we do not have.

The *eventual* gap this suite also closes — once its later layers land — is that
the integration spines run **server-only** (`NullTransport`, one store-set) and
enter at `engine.Commands.Execute(...)`, below the user's boundary. A real player
submits an *intent* from a client; the server decides; mutations cross the wire;
both peers converge; reactions fire. That is the destination, reached in stages.

## Locks

### Baseline locks (apply now)

- **L1 — Pictures are canonical.** Free-prose markdown (Goal / Given / When / Then /
  Invariants). No Gherkin rigidity, no exact-match steps, no step-definitions.
- **L2 — AI at author-time, determinism at run-time.** The LLM *writes* and *audits*
  tests; it never decides pass/fail on a run. The committed NUnit test is the gate.
  Any LLM step (generate, drift-check) is **local/advisory and never gates CI** —
  CI runs only the deterministic NUnit bindings.
- **L3 — Through the real composition root.** Boot via the actual `Bootstrap` layers
  ("as if Play was pressed"). Never hand-wire what the layers provide. (Hand-wiring
  stays in unit / `EngineStack`; acceptance is the DI style, scaled up.)
- **L6 — One picture ↔ one test, backlinked, deterministic oracle.** Exact expected
  state. No toolchain dependency (no Cucumber / SpecFlow / Reqnroll). The backlink
  is explicit (an attribute or front-matter id), not a fragile naming convention —
  pinned in Phase 1.

### Deferred locks (north star — activate when their layer lands)

These are the destination. They are **not** baseline constraints; they switch on
as their layer is built, and at that point a scenario is not "acceptance-complete"
until it satisfies them.

- **L4 — Enter at the Submit seam.** Drive at the client-intent seam (`Submit`),
  not `Execute`. *Activates when the client-submission layer lands (L+1).* Until
  then the entry seam is `Execute`, explicitly interim.
- **L5 — Two real sides.** Assert server↔client convergence across **separate
  store-sets** fed by a transport. A one-sided "loop" is not the loop.
  *Activates when the client + transport layer lands (L+2).*

### Prerequisite to watch — deterministic seeding

The exact-oracle premise depends on **all randomness being seeded** by the
harness. The baseline command scenarios have no RNG, so this is not yet binding —
but the moment a scenario touches a shuffle/RNG, deterministic seeding becomes a
hard requirement (an unseeded source means the scenario is not bindable).

## Artifact layout (provisional — confirmed in Phase 1)

```
Assets/Framework/Acceptance/
  Scenarios/                     # the pictures — NL, canonical spec
    _how-to-bind.md              # generator contract (the prompt, written once)
    deal-damage-reduces-health.md
  Tests/                         # generated deterministic NUnit bindings
    DealDamageReducesHealth.cs
  Harness/                       # MatchHarness (server-only at first; grows)
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
- <match started as if Play was pressed; N players; starting state, seeded>

When:
- <the single command executed (baseline); later: the intent a player submits>

Then:
- <mutations apply through the real path; resulting state is exactly X>

Invariants:
- <what must always hold — e.g. illegal command emits ZERO wire events>
```

## Phased plan

Each phase lands green (headless batch run) before the next.

### Phase 1 — Convention + scaffolding (baseline in place)

**Ships:** the `Scenarios/` picture format; the `_how-to-bind.md` generator
contract; the explicit picture↔test backlink mechanism; the
`Framework.Acceptance.Tests` asmdef; a **thin server-only `MatchHarness`** —
boots the real composition root, exposes `Seed(...)`, `Execute(...)`, and reads
via the read-only facades (a picture-bound wrapper over what
`BootstrapWiringIntegrationTests` already does). One **trivial** picture bound to
one deterministic test.

**Proof:** one picture ↔ one green NUnit test; the convention round-trips
end-to-end through the real composition root.

**Settles:** L1 / L2 / L3 / L6 + the artifact layout.

### Phase 2 — Real command scenarios at the `Execute` seam

**Ships:** the high-value command set, each picture-bound, single-side, through
the real composition root. Grow the set one scenario at a time:

1. **deal damage** — read-modify-write through a stateful validator; exact oracle
   (e.g. `30 → 18`). *This is the gate for Phase 2.*
2. **illegal command → zero wire traffic** — validator rejects before the block
   opens; state untouched.
3. **multi-mutation under one block** — one `Execute`, several mutations bracketed
   by one Command block.

**Proof:** N pictures ↔ N green subcutaneous tests validating a command in a real
flow — from the command's intake (`Execute`) through execution, mutation, and
read-back.

**Note:** scenario (3) is the descoped form of the command-service spike's
**Phase 3** (multi-mutation/ordering) — proven single-side here as a fast,
isolated regression. It is re-proven two-sided later (L+2) without throwing the
single-side test away.

## Deferred ladder (later layers — one at a time)

Built **after** the baseline (Phases 1–2) is solid. Each is its own deliberate
step, validated before the next.

- **L+1 — Client-submission seam (`Submit`).** Net-new substrate, **not** test
  plumbing: how an intent serializes, how the server receives an inbound client
  message, validates, and decides — the inverse of the `RequestService` archetype.
  Deserves its own spike (mirroring `request-service.md`). Activates **L4**;
  raises the suite's entry point from `Execute` up to `Submit`. *Resolves the
  command-service spike's deferred client-submission seam.*
- **L+2 — Two-sided convergence.** Asserts server↔client convergence across
  separate store-sets. First question to settle: is headless two-sided convergence
  reachable in **EditMode** via `LoopbackTransport.Pair()` (the in-memory paired
  transport already exists), or does it need **PlayMode**? Minimal two-peer proof
  first. Activates **L5**. Re-proves the multi-mutation/ordering scenario two-sided.
- **L+3 — Reactions in the loop.** Pictures covering follow-on effects: command →
  reaction fires → more mutations → both sides converge; the reaction fires
  **exactly once**, not per-side. Granularity here is "one triggering command/intent
  **+ its full causal closure**," not a whole match. Depends on reaction/SBA work.
- **Living-doc hygiene (matures alongside).** Author-time LLM **drift check**
  ("does this test still satisfy its picture?") — **local/advisory, never a CI
  gate** — plus a scenario index. Keeps L1/L2 honest over time.

## Relationship to the command-service spike

- The baseline (Phases 1–2) exercises exactly what command-service Phase 0–2
  shipped: `Execute` → validate → block → handler → mutation → read-back, with a
  stateful validator deciding on live state.
- **L+1** resolves its deferred **client-submission** seam (intents enter from a
  client; the server decides).
- The multi-mutation/ordering proof (command-service **Phase 3**) lands single-side
  in Phase 2 (scenario 3) and is strengthened to **server→client** at **L+2** — the
  merit-based reorder we agreed: flow-fidelity grows on top of, not instead of, the
  isolated single-side proof.

## Open decisions

- **Picture location** — provisional `Assets/Framework/Acceptance/Scenarios/`
  (with the tests) vs `Product/`. Confirm in Phase 1.
- **Backlink mechanism** — attribute (e.g. `[Picture("deal-damage-reduces-health.md")]`)
  vs front-matter id. Pin in Phase 1; must be robust for the drift-check.
- **Scenario granularity** — "not too wide": **one command through the real flow**
  (baseline), growing to **one triggering intent + its full causal closure** once
  reactions land. A scenario is one coherent play, not a whole match.
- **Convergence tier** — EditMode (`LoopbackTransport`) vs PlayMode. Settled at L+2.

## Explicitly out of scope / deferred

- **UI projection tests** (humble-object: feed a flow-state to the UI, assert render;
  capture emitted intents). Deferred until a UI exists. Bank the constraint now:
  **the UI holds no logic** — the subcutaneous flow is the total system, the UI is a
  thin projection over it.
- **Run-time LLM-as-judge validation.** Excluded by design. Our engine is
  deterministic with exact oracles; LLM judging belongs to non-deterministic output
  (LMUnit's domain), not here. The only LLM at run scope is *none*; the LLM operates
  at author time (generate + drift-check) only.
</content>
</invoke>
