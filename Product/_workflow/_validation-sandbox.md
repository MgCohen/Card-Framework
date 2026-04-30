---
type: sandbox
last-updated: 2026-04-30
tags: [#validation]
---

# Debt-Pass Validation Sandbox

This file is a fixture for `/builder:debt-pass` Layer 2 dry runs. It contains five retiring terms in prose form so the discharge loop has real text to edit. Do not link to or reference this file from real Product/ docs — it exists only to be used (and rewritten) by the validation runbook.

The sandbox terms (`OldFooBar`, `LegacyBaz`, `StaleQux`, `DeprecatedZed`, `RetireWidget`) are deliberately weird so they cannot collide with any real cascade entry.

## §1 OldFooBar overview

The `OldFooBar` component handles the foo path. When `OldFooBar` receives a request, it routes through the legacy adapter and emits a `OldFooBar.Resolved` event downstream.

## §2 LegacyBaz integration

`LegacyBaz` wires into the `OldFooBar` pipeline at the seam. Callers reference `LegacyBaz` directly today; the contract has not yet been re-shaped.

## §3 StaleQux semantics

`StaleQux` is the read-side accessor. Its shape needs reconsidering in light of the §1 changes — the slice boundary it implies no longer maps cleanly onto the new model.

## §4 DeprecatedZed lifecycle

`DeprecatedZed` manages the lifecycle hooks. Three phases: init, run, teardown. The init phase fires before any `DeprecatedZed`-owned scope is established.

## §5 RetireWidget surface

`RetireWidget` exposes the public API surface. Used by the §2 integration code and indirectly by §4 during teardown.
