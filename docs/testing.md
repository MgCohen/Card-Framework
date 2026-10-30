# Testing

How tests are organized in this repo, which kind to write for a given job, and the
rules for keeping them honest. For *how to run* tests, see the headless batch command
in [CLAUDE.md](../CLAUDE.md) (§Code → "Verify before claiming done").

## The three tiers

| Tier | Where | Proves | Authored as |
|---|---|---|---|
| **Unit** | `Assets/<Feature>/Tests/` (`Framework.<Feature>.Tests`) | one feature's component contracts in isolation | hand-written code |
| **Acceptance** | `Assets/Testing/Acceptance/` (`Framework.Testing.Acceptance.Tests`) | a real gameplay moment reads back as the spec says — **intent/behaviour** | prose **picture** → generated **binding** → human approves |
| **Integration** | `Assets/Testing/Integration/` (`Framework.Testing.Integration.Tests`) | the parts wire and compose — **mechanism/structure** | hand-written code |

Unit tests are **per-feature and local** — each feature owns its own sample types and
its `Tests/` asmdef references only that feature. They are deliberately not folded into
the shared module below (coupling a unit island to a global helper would break its
standalone testability). The two **global** tiers — Acceptance and Integration — share
one neutral support module:

```
Assets/Testing/
├── Support/        Framework.Testing.Support            # shared DSL + fixtures (no tests)
│   ├── MatchHarness.cs        # boots the real composition root; the Execute seam + read facades
│   ├── MatchPair.cs           # server + client GameplayLayer over a loopback transport; convergence checks
│   ├── PictureAttribute.cs    # [Picture("x.md")] — backlinks a binding to its canonical spec
│   ├── SampleTags.cs          # canonical sample tags (slots 2001-3)
│   └── SampleCommands.cs      # sample commands + handlers + validators (public)
│
├── Acceptance/     Framework.Testing.Acceptance.Tests   # INTENT — prose-spec → generated binding
│   ├── Scenarios/             # the canonical specs (pictures) — you write these
│   │   └── _how-to-bind.md    #   the generator contract, stated once
│   └── Bindings/              # the derived deterministic tests — generated, you approve
│
└── Integration/    Framework.Testing.Integration.Tests  # MECHANISM — hand-written code
    ├── EngineStack.cs                     # no-DI hand-wire (proves the types compose)
    ├── FullFlowIntegrationTests.cs        # end-to-end spine over EngineStack
    ├── BootstrapWiringIntegrationTests.cs # real-DI compose + request round-trip
    ├── RealAppFlowRootCompositionTests.cs # literal root via reflection — nesting-bug guard
    └── DrawCardFixtures.cs                # request/response machinery (Requests/Scaffold confined here)
```

## Which tier — the decision rule

Two questions decide it:

1. **What does the test prove?** A real gameplay moment behaving as specified
   (*intent*), or that the parts wire/compose (*mechanism*)?
2. **Would a non-technical reader care about the statement?** If yes, it's a behaviour
   you can write as a prose **picture**. If the only meaningful description is a
   paraphrase of the code ("the layer registers the handler", "the root resolves the
   engine"), it's a mechanism **tripwire**.

So:

- **Behaviour a player/designer would recognise** → **Acceptance picture.** Health goes
  30 → 18; an illegal move changes nothing; one command applies several mutations.
- **Wiring, composition, registration, a forgotten `Register` *throwing*, reflection over
  the scope tree** → **Integration** (hand-written). There is no narrow declarative seam
  to transcribe; a "spec" that must change every time the mechanism changes is a mirror,
  not a spec.
- **One feature's contract in isolation** → **Unit**, in that feature's own `Tests/`.

The rule in one line: **if it can be a picture, it is one (Acceptance); what's left is
hand-written because it's a tripwire on internals (Integration).**

### Why two authoring styles, not one

Behaviour maps onto a declarative spec through a **narrow, stable public seam**
(`MatchHarness.Execute` + read facades): prose transcribes mechanically, the generator
has little latitude, and the picture survives refactors. Mechanism has no such seam — it
asserts on construction and on negatives (a missing registration throws), so as prose it
degrades into a restatement of the code. The two tiers differ in *how they're authored*
**because** they differ in *what they prove* — a consequence, not an accident. This is
also why `BootstrapWiringIntegrationTests` asserts only that the graph wires and routes:
the command-behaviour oracles it used to carry (the 30 → 18 read-modify-write, the reject
reasons) moved out to Acceptance pictures, leaving Integration as pure mechanism.

## Authoring an Acceptance test (picture → binding → approve)

The full generator contract lives in
[`Assets/Testing/Acceptance/Scenarios/_how-to-bind.md`](../Assets/Testing/Acceptance/Scenarios/_how-to-bind.md)
— stated once, reused by every picture. The `author-acceptance-test` skill operationalises
it. In short:

1. **Write the picture** — a free-prose markdown spec in `Scenarios/`
   (Goal / Given / When / Then / Invariants). This is the **canonical** artifact.
2. **Generate the binding** — a deterministic NUnit test in `Bindings/` that boots the
   real composition root via `MatchHarness.Start(...)`, seeds the Given, acts the When
   through `match.Execute(...)`, and asserts the Then as an **exact oracle**.
3. **Backlink** — the binding carries `[Picture("<file>.md")]`; the picture ends with
   `Bound by: \`Bindings/<File>.cs\``.
4. **Approve** — run headless, read the result, and a human keeps or rejects the binding.

The LLM **writes and audits at author time only**. It never decides pass/fail on a run —
the committed NUnit assertion is the gate. Our engine is deterministic (event-sourced,
exact oracles like `health == 18`), so validation is by code assertion, never an
LLM judge. Any LLM step (generate, drift-check) is local/advisory and never gates CI.

## What to touch — and what not to

- **The picture is canonical.** When behaviour changes, edit the picture first, then
  **regenerate** the binding from it. Don't hand-patch a binding and leave its picture
  stale — that inverts the source of truth.
- **Mechanism tests are intentional tripwires.** A change that makes
  `BootstrapWiring`/`RealAppFlowRoot` fail is telling you the wiring changed. Fix the
  wiring or update the test deliberately — don't paper over it.
- **Use `MatchPair` when the client matters.** It starts a server and a client
  `GameplayLayer` joined by `LoopbackTransport.Pair`. Register apply handlers on both sides
  and commands on the server only (`bindScenario(builder, isServer)`). Call
  `AssertStreamsMatch()` to check that the client got the same events.
- **Check `Support/` before inventing a fixture.** The shared seam (`MatchHarness`),
  sample commands/handlers/validators (`SampleCommands.cs`), and sample tags
  (`SampleTags.cs`) already exist. Reuse them. Only add a new sample type when no existing
  one fits — and add it to `Support/`, public, so both tiers share it.
- **Share construction tooling, never constructed state.** `Support/` is *builders and
  sample types* (a [Test Helper](http://xunitpatterns.com/Test%20Helper.html) /
  [Test Data Builder](http://www.natpryce.com/articles/000714.html)), not a shared live
  world. Each test builds its own match via `MatchHarness.Start(...)`
  ([Fresh Fixture](http://xunitpatterns.com/Fresh%20Fixture.html)); a
  [Shared Fixture](http://xunitpatterns.com/Shared%20Fixture.html) is an anti-pattern.
- **Per-feature unit fixtures stay local.** Don't reach into `Testing/Support`
  from a feature's `Tests/` assembly, and don't fold a feature's sample types into Support.

## Assembly fences (compiler-enforced boundaries)

The asmdef references *are* the boundary — the public-surface fence rides on them, not on
folder location:

- **`Framework.Testing.Support`** — references the **public surface only** (Bootstrap,
  Commands, Core, Wire, Tags, Entities, Engine, VContainer, nunit). **No** Requests/States/
  Scaffold, **no** `InternalsVisibleTo` grant.
- **`Framework.Testing.Acceptance.Tests`** — references Support + the public surface. **No**
  Requests/States/Scaffold, **no** internals grant. This keeps the bindings **subcutaneous**
  by construction: they can only reach the engine through its public seam, so the
  structural proof "Acceptance drives the real public API" is enforced by the compiler.
- **`Framework.Testing.Integration.Tests`** — references Support + Requests/States/Scaffold,
  and **receives** the four `InternalsVisibleTo` grants (from `Commands`/`Entities`/
  `Requests`/`Tags` `Runtime/AssemblyInfo.cs`). Mechanism tests are allowed to touch
  internals; that's their job.

If you add a global test that needs a feature's internals, it belongs in **Integration**
(and that feature must grant `InternalsVisibleTo("Framework.Testing.Integration.Tests")`).
If it only needs the public seam, prefer **Acceptance** — and keep the fence intact.

## Rulebook and gate

Architecture and test rules are data in `Tools/Rulebook/rulebook.json`. Each rule names a check
kind, and `Tools.Rulebook.Tests` runs one test case per rule. Run everything with one command:

```powershell
powershell -NoProfile -File Tools/Rulebook/gate.ps1
```

- **A test that proves a rule** carries `[Rule("ID")]` from `Tools.Rulebook`. The gate reports it
  under that rule, and the rule fails if it uses the `attached` kind and no test links to it.
- **A new structural rule** (dependencies, who may call what, required pairings) goes in the
  rulebook, not only in this file. If no check kind fits, add one: see
  [`Tools/Rulebook/README.md`](../Tools/Rulebook/README.md).
- **A known violation** goes in the rule's `exceptions` with a reason and the work that removes it.
  The rule fails when an exception goes stale.

## Prior art

The intent/mechanism split, and the choice to AI-generate the behaviour specs but
hand-write the mechanism tests, is grounded in independent sources:

- **[Acceptance-test generation with LLMs — industrial case study (arXiv 2504.07244)](https://arxiv.org/html/2504.07244v1)**
  — NL spec → executable script, mandatory human review, the LLM never judges at run-time;
  scoped to acceptance, explicitly not integration. Mirrors our pipeline.
- **[Cucumber — Writing better Gherkin](https://cucumber.io/docs/bdd/better-gherkin/)** +
  **[anti-patterns](https://cucumber.io/blog/bdd/cucumber-antipatterns-part-one/)** —
  scenarios describe intended behaviour, not implementation; using specs for technical
  tests "misses the intent of BDD." Human-readable spec when a non-technical reader cares;
  code otherwise.
- **[Farley — acceptance-testing four-layer model](https://thinkinglabs.io/notes/2021/07/18/acceptance-testing-for-continuous-delivery-dave-farley.html)**
  — Specification → DSL → Driver → System. `MatchHarness` is that DSL/driver layer, which
  is why it lives in `Support/`.
- **[Fowler — practical test pyramid / subcutaneous test](https://martinfowler.com/articles/practical-test-pyramid.html)**
  — our bindings are textbook subcutaneous (below the UI, through a seam); acceptance is
  orthogonal to the pyramid, not a higher layer of mechanism.
- **Sharing:** [GOOS](https://growing-object-oriented-software.com/),
  [Test Data Builder](http://www.natpryce.com/articles/000714.html),
  [Test Helper](http://xunitpatterns.com/Test%20Helper.html) — share construction tooling;
  the [Shared Fixture](http://xunitpatterns.com/Shared%20Fixture.html) is the anti-pattern.

*Honest caveat:* the intent/mechanism line is a value judgment, not a law — Gherkin *can*
drive any level. The objection is only that spec-authoring loses its point with no
non-technical reader and no business behaviour. The "mechanism test = intentional tripwire"
framing is our synthesis, aligned with "test behaviour, not implementation."

## Background

The full design history, the deferred north-star layers (client-`Submit` seam, two-sided
convergence, reactions), and the deferred test-freshness validator agent were in the spike
plan `Product/spikes/acceptance-test-suite.md`, deleted on 2026-10-30. It is in git at commit `b13907e`.
