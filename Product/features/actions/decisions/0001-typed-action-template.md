---
type: adr
status: accepted
tags: [#adr, #actions, #snapshot, #templates]
---

# ADR-0001 — Typed ActionTemplate per command type

Date: 2026-04-29
Context-doc: features/actions/models/02-availability-snapshot.md
Proposal: features/actions/models/02-availability-snapshot.md, features/engine/models/03-snapshot-pipeline.md

## Context
The snapshot needs a way to describe "what command can be issued, against what targets, with what choices." Earlier sketches used a string-keyed `ActionId` plus a slot dictionary; this turned IntentMapper into a reflection-flavored switch and made composition awkward.

## Options considered
- A: String-keyed `ActionId` + slot dictionary — rejected for being stringly-typed and hostile to evolution.
- B: Generic `ActionTemplate<TCommand>` only (no abstract base) — rejected because templates must be storable in a uniform list.
- C: Shared abstract base `ActionTemplate` (`CommandType: Type`, `Source: EntityId?`, targets `IReadOnlyList<EntityId>`) with a typed subtype per command. Chosen.

## Decision
Each command type has a paired `ActionTemplate` subtype. The base carries the command-type discriminator and shared fields. IntentMapper switches on subtype; no string-keyed lookup.

## Consequences
- Enables compile-time IntentMapper safety.
- Enables `ActionId` retirement (the `Type` is the identifier).
- Constrains: every new command type ships a paired template; the cost is small and uniform.
