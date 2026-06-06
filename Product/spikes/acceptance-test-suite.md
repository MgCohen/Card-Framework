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

## Artifact layout (settled in Phase 1)

> **Superseded (2026-06-05) by *Suite consolidation* below.** The Phase-1 sibling
> layout (`Assets/Acceptance/`, parallel to `Framework/`) shipped as written and is
> kept here as the historical baseline. The suite now folds into
> `Assets/Framework/Testing/` with a shared `Support/` module; Acceptance keeps its
> public-surface fence via asmdef references, not via top-level location.

The suite lives **parallel to** `Framework/` — a sibling top-level assembly that
*reads* the framework but that the framework never references (one-way dependency,
the way `Scaffold.*` sits parallel). It is not a framework feature; it observes the
whole framework from outside.

```
Assets/Acceptance/
  Scenarios/                     # the pictures — NL, canonical spec
    _how-to-bind.md              # generator contract (the prompt, written once)
    set-health-records-the-value.md
  Harness/
    MatchHarness.cs              # boots the real composition root; the Execute seam
    PictureAttribute.cs          # the picture↔test backlink
  Tests/
    Fixtures.cs                  # scenario-local sample command + tag
    SetHealthRecordsTheValue.cs  # the deterministic binding
  Acceptance.Tests.asmdef        # references framework runtime only
```

Pictures live **with** the test assembly (versioned next to their bindings,
discoverable), not in `Product/`. The generator contract (`_how-to-bind.md`) is the
prose analog of BDD's step-definition library — stated **once**, reused by every
picture. The backlink is an explicit `[Picture("<file>.md")]` attribute (an NUnit
property, so it surfaces in test results) — not a naming convention.

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

## Suite consolidation (Framework/Testing/)

> Decided 2026-06-05, **before** implementation. Folds the two **global** test
> tiers — Acceptance (intent) and Integration (mechanism) — under one
> `Assets/Framework/Testing/` area sharing a neutral `Support/` DSL + fixtures
> module. Per-feature unit assemblies and Spike tests are untouched.

### The two axes — why we keep both patterns

- **Axis 1 — what a test proves:** *intent/behavior* (a real gameplay moment reads
  as the spec says) vs *mechanism/structure* (the parts wire/compose).
- **Axis 2 — how it's authored:** prose **picture → generated binding → human
  approves** (SDD) vs **hand-written code**.

The patterns differ on Axis 2 **because** they differ on Axis 1 — a consequence,
not an accident. Behavior maps onto a declarative spec through a **narrow, stable
public seam** (`MatchHarness.Execute` + read facades): prose transcribes
mechanically, the LLM has little latitude, and the spec survives refactors.
Mechanism has no such seam — it asserts on construction, on negatives (a forgotten
`Register` *throws*), and on reflection over the scope tree. Expressed as prose it
becomes a paraphrase of the code, and a "spec" that must change every time the
mechanism changes is a mirror, not a spec. So the rule: **if it can be a picture, it
is one (Acceptance); what's left is hand-written because it's a tripwire on internals
(Integration).** This is exactly what Phase 2's trim does — behavior that leaked into
`BootstrapWiring` (the `30→18` oracle, etc.) moves out to pictures, leaving Integration
as pure mechanism.

### Prior art (so we don't re-derive it)

Independent, credible sources land on this same split:

- **[Acceptance Test Generation with LLMs — industrial case study (arXiv 2504.07244)](https://arxiv.org/html/2504.07244v1).**
  Two-step pipeline (NL Gherkin scenario → executable script), mandatory human review
  before keep, LLM generates static code and **never judges at run-time**, NL spec
  canonical and guides implementation — our pipeline, beat-for-beat. Scoped to
  acceptance only; explicitly notes "no research on the use of LLMs in integration
  testing." The team that industrialized our exact workflow drew our exact line.
- **[Cucumber — Writing better Gherkin](https://cucumber.io/docs/bdd/better-gherkin/)**
  + **[Cucumber anti-patterns](https://cucumber.io/blog/bdd/cucumber-antipatterns-part-one/).**
  "Scenarios should describe the intended behaviour … not the implementation";
  implementation detail belongs in step-definition *code*. Using specs for
  unit/technical tests "misses the intent of BDD." Deciding rule: human-readable spec
  when a non-technical reader cares; code when it's developer-facing.
- **[Farley — acceptance-testing four-layer model](https://thinkinglabs.io/notes/2021/07/18/acceptance-testing-for-continuous-delivery-dave-farley.html).**
  *Specification → DSL → Driver → System*: specs decoupled via a domain DSL stay stable
  across refactors. `MatchHarness` **is** that DSL layer (→ it belongs in `Support/`).
- **[Fowler — practical test pyramid / subcutaneous test](https://martinfowler.com/articles/practical-test-pyramid.html).**
  Our bindings are textbook subcutaneous (below UI, through a seam); acceptance is
  orthogonal to the pyramid — confirming intent ≠ a higher layer of mechanism.
- **Sharing:** share *construction tooling* (builders / DSL / sample types) across
  tiers — endorsed ([GOOS](https://growing-object-oriented-software.com/),
  [Test Data Builder](http://www.natpryce.com/articles/000714.html),
  [Test Helper](http://xunitpatterns.com/Test%20Helper.html)). Never share
  *constructed state* — the [Shared Fixture](http://xunitpatterns.com/Shared%20Fixture.html)
  anti-pattern; each test builds its own ([Fresh Fixture](http://xunitpatterns.com/Fresh%20Fixture.html)).
  → motivates the neutral `Support/` module.

**Caveat (honest):** it's a value judgment, not a prohibition — Gherkin *can* drive
any level; the objection is that spec-authoring loses its point with no non-technical
reader and no business behavior. The "mechanism test = intentional tripwire" framing
is our synthesis (aligned with "test behavior, not implementation"), not a quote.

### Target structure

```
Assets/Framework/Testing/
├── Support/                              # shared DSL + primitives — one neutral module both tiers build on
│   ├── Framework.Testing.Support.asmdef        #   refs: public surface only (Bootstrap, Commands, Core, Wire,
│   │                                            #   Tags, Entities, Engine, VContainer, nunit). NO Requests/States/Scaffold.
│   ├── MatchHarness.cs                   #   the domain DSL/driver: Start(bind) → Execute(cmd) → read facades
│   ├── PictureAttribute.cs               #   [Picture("x.md")] — backlinks a binding to its canonical spec
│   ├── SampleTags.cs                     #   canonical sample tags (slots 2001-3; the 3001-3 set retired)
│   └── SampleCommands.cs                 #   sample commands + handlers + validators (public)
│
├── Acceptance/                           # INTENT — prose-spec → generated binding, public seam only
│   ├── Framework.Testing.Acceptance.Tests.asmdef   # refs: Support + public surface + runners.
│   │                                            #   NO Requests/States/Scaffold, NO internals grant → fence compiler-enforced
│   ├── Scenarios/                        #   the canonical specs — you write these; the LLM reads them
│   │   ├── _how-to-bind.md               #     the generator contract (4 beats)
│   │   ├── set-health-records-the-value.md
│   │   ├── deal-damage-subtracts-from-health.md
│   │   ├── illegal-command-emits-nothing.md
│   │   └── one-command-applies-several-mutations.md
│   └── Bindings/                         #   the derived deterministic tests — LLM-generated, you approve
│       ├── SetHealthRecordsTheValue.cs
│       ├── DealDamageSubtractsFromHealth.cs
│       ├── IllegalCommandEmitsNothing.cs
│       └── OneCommandAppliesSeveralMutations.cs
│
└── Integration/                          # MECHANISM — hand-written code, allowed to touch internals
    ├── Framework.Testing.Integration.Tests.asmdef  # refs: Support + Requests, States, Scaffold.AppFlow,
    │                                            #   Scaffold.Autopacker, AutoPackerContracts.dll + runners; receives the grant
    ├── EngineStack.cs                    #   no-DI hand-wire (proves the types compose)
    ├── FullFlowIntegrationTests.cs       #   end-to-end spine over EngineStack
    ├── BootstrapWiringIntegrationTests.cs#   real-DI compose (Phase 2 trims the behavior oracles it shares with Acceptance)
    ├── RealAppFlowRootCompositionTests.cs#   literal root via reflection — nesting-bug regression guard
    └── DrawCardFixtures.cs               #   request/response machinery — keeps Requests/Scaffold confined here
```

`Scenarios/` vs `Bindings/` makes the *spec-is-canonical, test-is-derived* relation
physical. Namespaces: `Framework.Testing` (Support), `Framework.Testing.Acceptance`
(bindings), `Framework.Testing.Integration` (spines).

### Assembly fences (the load-bearing boundaries)

- **`Framework.Testing.Support`** — public surface only; **no** Requests/States/Scaffold,
  **no** internals grant. Sample handlers/validators/`SampleTags` bumped to `public` so
  cross-assembly tests can register/read them.
- **`Framework.Testing.Acceptance.Tests`** — refs Support + public surface; **no**
  Requests/States/Scaffold, **no** `InternalsVisibleTo` → the public-seam fence stays
  **compiler-enforced** (the structural proof bindings are subcutaneous).
- **`Framework.Testing.Integration.Tests`** — refs Support + Requests/States/Scaffold;
  **receives** the four `InternalsVisibleTo` grants, renamed from `Framework.Integration.Tests`
  (in `Commands`/`Entities`/`Requests`/`Tags` `Runtime/AssemblyInfo.cs`).

### Execution plan

Branch `claude/test-suite-consolidation`, cut from `main` after PR #181 merges → one
PR back into `main`. Each phase = one commit; green (headless batch) before the next.

- **Phase 0 — Sequence & baseline.** Merge #181 into `main`; cut the branch from fresh
  `main`; headless baseline confirms **364/364** before touching anything.
- **Phase 1 — Structural move + de-dup (zero assertion changes).** Create `Support/`
  (asmdef + `MatchHarness`/`PictureAttribute` moved, namespace `Acceptance`→`Framework.Testing`);
  consolidate the two duplicated fixtures into `Support/SampleTags.cs` + `SampleCommands.cs`
  (slots 2001-3), **bump `SampleTags` + the three handlers + `DealDamageValidator` to public**,
  delete both old fixture files; create `Acceptance/` (asmdef, `Scenarios/` + `Bindings/`),
  create `Integration/` (asmdef, spines + `DrawCardFixtures`); **rename the four
  `InternalsVisibleTo` grants**; delete the old `Assets/Acceptance/` and
  `Assets/Framework/Integration/` trees. Headless **must still be 364/364**. Commit.
- **Phase 2 — Redundancy trim.** Cut `BootstrapWiring` steps 1b/1c/4b (the `30→18`,
  rejected-emits-nothing, `NotDamageable` oracles now owned by Acceptance); keep the
  mechanism-only spine (compose → handlers/validators resolve → request open→answer→resolve).
  Headless green. Commit.
- **Phase 3 — Documentation + reusable authoring skill.** Write **`docs/testing.md`**
  (taxonomy + decision rule "non-technical reader cares → picture; how it's wired →
  integration"; the picture→binding→approve workflow; *what to touch vs not* — picture
  is canonical, regenerate rather than hand-edit bindings, mechanism tests are tripwires;
  **check `Support/` first** before inventing fixtures, Fresh-not-Shared; the asmdef
  fences; rationale + the prior-art links above). Update `docs/feature-layout.md` and the
  `CLAUDE.md` test section to point at `Framework/Testing/` + the new assembly names.
  Add skill **`author-acceptance-test`** — operationalizes `_how-to-bind.md`: from a
  described gameplay moment, scaffold the picture + binding (correct `[Picture]` backlink,
  `MatchHarness` seam, exact oracle), then prompt to run headless + approve. *May split
  into its own follow-up PR so the structural change merges clean.* Commit.
- **Phase 4 — Verify & PR.** Full headless run + spot-check both new assemblies resolve;
  open PR into `main` (summary, two-axis rationale, literature links, before/after tree).

### Deferred (saved to memory, not in this PR)

- **Test-freshness validator agent** — advisory drift check between pictures and bindings
  (and `Support` fixtures vs their consumers); author-time only, never gates CI. Picks up
  after this lands. (Recorded in user memory: `project_test_freshness_validator_deferred.md`.)

### Untouched by design

Per-feature `Framework/<Feature>/Tests/` assemblies keep their own local sample types —
deliberate isolation, not duplication to fix (coupling a unit island to a global Support
module would break standalone testability). All `Spike/` tests (incl. the Benchmark gate)
and PlayMode tests (separate platform) are left alone.

## Open decisions

- ~~**Picture location**~~ — settled in Phase 1 (`Assets/Acceptance/Scenarios/`,
  sibling to `Framework/`); **re-settled 2026-06-05** by *Suite consolidation* to
  `Assets/Framework/Testing/Acceptance/Scenarios/`. The public-surface fence now rides
  on asmdef references (no internals grant), not on top-level location.
- ~~**Backlink mechanism**~~ — settled in Phase 1: the `[Picture("<file>.md")]`
  attribute (an NUnit property), not a naming convention.
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
