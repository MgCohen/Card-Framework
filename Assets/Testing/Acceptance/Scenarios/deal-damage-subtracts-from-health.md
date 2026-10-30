# Dealing damage subtracts from a unit's health

Goal: prove a command can read live world state, decide on it, and write a value
derived from it — a stateful validator gates the move, the handler does the
read-modify-write, and the result reads back exactly.

Given:
- a match started as if Play was pressed (server-side)
- a unit in play with health recorded as 30

When:
- a DealDamage command executes for that unit with amount 12

Then:
- the command is accepted (the validator sees a damageable target)
- the unit's health reads exactly 18

Invariants:
- the validator decides on live state read through the real engine, not a seeded stub
- the result is the read-modify-write of the prior value, not an overwrite

Bound by: `Bindings/DealDamageSubtractsFromHealth.cs`
