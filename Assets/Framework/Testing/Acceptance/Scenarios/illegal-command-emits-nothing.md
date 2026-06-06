# An illegal command changes nothing and puts nothing on the wire

Goal: prove a command rejected by its validator is fully inert — the block never
opens, no mutation applies, and not a single event reaches the wire.

Given:
- a match started as if Play was pressed (server-side)
- a unit in play with no health recorded (not a damageable target)

When:
- a DealDamage command executes for that unit

Then:
- the command is rejected with reason "NotDamageable"
- the unit still has no health recorded

Invariants:
- a rejected command emits ZERO wire events — the validator gates before the block opens
- world state is byte-for-byte untouched by the rejected command

Bound by: `Bindings/IllegalCommandEmitsNothing.cs`
