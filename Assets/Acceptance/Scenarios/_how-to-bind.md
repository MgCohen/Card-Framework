# How to bind a picture to a test

The generator contract, stated once and reused by every picture. A **picture** is
the canonical spec (free-prose markdown); its **binding** is a deterministic NUnit
test that drives the real engine and asserts an exact oracle. The LLM writes and
audits bindings at author time; it never decides pass/fail on a run.

## The picture

```
# <Scenario title — the real gameplay moment>

Goal: <one line — what real play this proves>

Given:
- <starting world state, seeded directly>

When:
- <the single command executed (baseline seam)>

Then:
- <the exact resulting state>

Invariants:
- <what must always hold>
```

## The binding

One picture ↔ one test, backlinked, deterministic. The test:

1. **Boots the real gameplay composition** via `MatchHarness.Start(...)` — the same
   `GameplayLayer` `FrameworkAppFlowRoot` installs "as if Play was pressed," composed
   flat into one scope. The harness builds that scope directly (`LifetimeScope.Create`)
   rather than through `FrameworkAppFlowRoot`/`AppFlowHost`, so the scenario's
   `bindScenario` registrations land in the same scope as the layer's installers — which
   is where the command registries collect them. The scenario's command
   handlers/validators are registered in the `bindScenario` callback
   (`builder.RegisterCommandHandler<...>()`, `builder.RegisterCommandValidator<...>()`).
2. **Seeds the Given** by writing world state directly through the engine's
   services (`match.Engine.Entities.Create(...)`, `match.Engine.Tags.Set(...)`).
   Setup is faked here — we start from the command, not from a player's intent.
3. **Acts the When** through the seam: `match.Execute(command)`. This is the single
   chokepoint; it enters at the server command today and rises to the player's
   `Submit` when that layer lands, without touching the bindings.
4. **Asserts the Then** as an **exact oracle** via the read facades
   (`match.Engine.Tags.Get(...)`, etc.). No fuzzy matching, no LLM judge.

### Backlink

The test carries `[Picture("<file>.md")]`, naming its picture. The class and method
read as the scenario title in PascalCase / Snake_Case.

### Disposal

`using var match = MatchHarness.Start(...)` tears the scope down at the end of the
test. One match per test; no shared state.

## Locks this contract keeps

- **Pictures are canonical** — the markdown is the spec; the test serves it.
- **Determinism at run-time** — exact oracles only; any LLM step (generate,
  drift-check) is local/advisory and never gates CI.
- **Through the real composition** — never hand-wire what the layer's installers provide.
- **One picture ↔ one test** — exact expected state, no toolchain dependency.

## Not yet in scope (deferred layers)

Today the seam is the **server command**, single-side. The client-`Submit` seam,
two-sided server↔client convergence (`AssertConverged`), and reactions in the loop
are later layers — added one at a time. A scenario is "acceptance-complete" against
those locks only once its layer has landed; until then `Execute` is the interim seam.
