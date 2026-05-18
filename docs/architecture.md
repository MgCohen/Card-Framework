# Architecture

> **Status: stub.** Filling in as the framework architecture stabilizes. Read graduated feature code under `Assets/Framework/` for ground truth; this doc captures cross-cutting invariants and rationale that aren't obvious from the code alone.

Structure follows [matklad's ARCHITECTURE.md pattern](https://matklad.github.io/2021/02/06/ARCHITECTURE.md.html): bird's-eye view, codemap, cross-cutting invariants.

## Bird's-eye view

*Fill in once the core pipeline + state model is locked. For now, see the spikes under `Assets/Spike/ECS/` for working sketches.*

## Codemap

Modules and how they connect. Updated as features graduate.

- `Assets/Framework/Wire/` — wire stream (event log + replay). Validation harness; first graduated feature.
- `Assets/Spike/ECS/*` — active exploration. Not yet promoted.

## Cross-cutting invariants

System-wide rules that span features. Edit with care — these are the load-bearing decisions.

**Pipeline entry is `Enqueue`, not `Execute` / `Submit`.** External callers enqueue intents, actions, requests, or events into a single pipeline. The naming reflects the future shape — a multi-type pipeline routed by kind — not the current command-only state. Don't rename this even if "Execute" reads more naturally for command-only usage today.
