# Setting a unit's health records the value

Goal: prove a command flows through the real engine and its mutation is readable —
the convention round-trips end to end.

Given:
- a match started as if Play was pressed (server-side)
- a unit in play with no health recorded yet

When:
- a SetHealth command executes for that unit with value 20

Then:
- the command is accepted
- the unit's health reads exactly 20

Invariants:
- the mutation flows through the real composition root, not a hand-wired stub

Bound by: `Tests/SetHealthRecordsTheValue.cs`
