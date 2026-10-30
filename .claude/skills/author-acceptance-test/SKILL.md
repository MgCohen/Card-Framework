---
name: author-acceptance-test
description: >
  Scaffold a Spec-Driven acceptance test for the Unity card framework from a described
  gameplay moment: write the canonical prose picture, generate its deterministic NUnit
  binding, wire the picture↔binding backlink, then run headless and hand off for approval.
  Use this whenever the user wants to add or update an acceptance test, prove a gameplay
  moment works, write a picture/scenario for the engine, bind a scenario to a test, or
  asks "add an acceptance test for <behaviour>". For mechanism/wiring tests (does the DI
  graph compose, does a registration resolve) use a hand-written Integration test instead —
  see docs/testing.md.
allowed-tools: Read, Write, Edit, Glob, Grep, Bash
---

# author-acceptance-test

You scaffold one **acceptance test** for the card framework: a canonical prose **picture**
plus its deterministic NUnit **binding**, backlinked, driving the **real composition root**
through `MatchHarness` and asserting an **exact oracle**. You write and audit at author
time; you never make the test judge pass/fail at run-time — the committed NUnit assertion
is the gate.

**Read first (canonical, do not duplicate their content here):**
- `Assets/Testing/Acceptance/Scenarios/_how-to-bind.md` — the generator contract.
- `docs/testing.md` — tiers, the decision rule, what-to-touch-vs-not, the fences.

## Step 0 — Confirm this is an acceptance test

Acceptance = **intent/behaviour** a player or designer would recognise, expressible as a
prose Given/When/Then through the public seam (`MatchHarness.Execute` + read facades).

If the thing to prove is **mechanism** — the DI graph composes, a layer registers a
handler, a missing `Register` throws, reflection over the scope tree — STOP. That is a
hand-written **Integration** test (`Framework.Testing.Integration.Tests`), not a picture.
Say so and redirect; do not force a picture onto wiring.

Determinism prerequisite: the scenario must have an **exact oracle**. If it depends on a
shuffle/RNG, the harness must seed it first — an unseeded source means the scenario is not
yet bindable. Flag this rather than asserting a fuzzy result.

## Step 1 — Pin down the gameplay moment

From the user's description, extract: the **Goal** (one line of real play), the **Given**
(starting world state, seeded directly), the single **When** (the command executed at the
`Execute` seam — baseline is server-side, single command), the **Then** (the exact
resulting state), and the **Invariants** (what must always hold, e.g. "a rejected command
emits ZERO wire events"). If any is ambiguous, ask before scaffolding.

## Step 2 — Check `Support/` before inventing anything

```
Assets/Testing/Support/SampleCommands.cs   # SetHealth/DealDamage/IntroduceCard + handlers + validators
Assets/Testing/Support/SampleTags.cs       # Health/Cost/IsCard (slots 2001-3)
Assets/Testing/Support/MatchHarness.cs      # the seam + read facades
```

Reuse an existing sample command/handler/validator/tag if one fits. Only add a new one
when none does — add it to `Support/`, **public** (cross-assembly), never as a test-local
duplicate. Share construction tooling; never share a constructed world (each test builds
its own match — Fresh Fixture).

## Step 3 — Write the picture

Create `Assets/Testing/Acceptance/Scenarios/<kebab-title>.md` using the template
from `_how-to-bind.md` (Goal / Given / When / Then / Invariants). End the file with:

```
Bound by: `Bindings/<PascalTitle>.cs`
```

The picture is the **canonical spec** — write it to read as living documentation, not as a
paraphrase of code.

## Step 4 — Generate the binding

Create `Assets/Testing/Acceptance/Bindings/<PascalTitle>.cs`:

- `namespace Framework.Testing.Acceptance;` (Support's public types resolve via the
  enclosing `Framework.Testing` namespace — no extra `using` needed for `MatchHarness`,
  `SampleTags`, the sample commands/handlers).
- Class + `[Test]` method named as the title in PascalCase / Snake_Case.
- `[Picture("<kebab-title>.md")]` on the method — the backlink (an NUnit property).
- Body, following `_how-to-bind.md`:
  1. `using var match = MatchHarness.Start(builder => { builder.RegisterCommandHandler<…>(); builder.RegisterCommandValidator<…>(); });`
  2. Seed the Given via `match.Engine.Entities.Create(...)` / `match.Engine.Tags.Set(...)`.
  3. Act the When via `match.Execute(command)`.
  4. Assert the Then as an **exact oracle** via the read facades
     (`match.Engine.Tags.Get(...)`, `result.Accepted`, `result.RejectReason`, and for
     zero-wire-traffic invariants, count `match.Engine.Stream.OnAppended`).

Mirror the existing bindings in `Bindings/` for shape and imports.

## Step 5 — Run headless and report

Run the affected assembly (do not claim done on "looks right"):

```
Unity.exe -batchmode -nographics -projectPath . -runTests \
  -testPlatform EditMode -testCategory "!Benchmark" \
  -assemblyNames Framework.Testing.Acceptance.Tests \
  -testResults Logs/test-results.xml -logFile Logs/test.log
```

Read `Logs/test-results.xml` for pass/fail and `Logs/test.log` for compile errors. Report
the result. The results XML can lag a few seconds after the process exits — poll for it.

## Step 6 — Hand off for approval

Show the picture and the binding and state the result. A **human keeps or rejects** the
binding — the LLM does not self-approve. If behaviour later changes, edit the **picture
first**, then regenerate the binding from it; never hand-patch a binding into disagreeing
with its picture.

## Locks (never violate)

- **Picture is canonical** — the markdown is the spec; the test serves it.
- **AI at author time, determinism at run-time** — exact oracles only; no LLM judge on a
  run; any LLM step is local/advisory and never gates CI.
- **Through the real composition root** — always `MatchHarness.Start(...)`; never hand-wire
  what the layer's installers provide.
- **One picture ↔ one test**, backlinked with `[Picture(...)]`, exact expected state.
- **Keep the fence** — Acceptance references only Support + the public surface (no
  Requests/States/Scaffold, no internals grant). If you need internals, it's an Integration
  test, not an acceptance test.
