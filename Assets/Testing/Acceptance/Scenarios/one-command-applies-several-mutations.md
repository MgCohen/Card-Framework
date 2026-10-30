# One command applies several mutations under a single block

Goal: prove a single accepted command can carry more than one mutation — the
framework brackets them under one Command block, and every mutation lands from
one Execute.

Given:
- a match started as if Play was pressed (server-side)
- a fresh card entity with no tags recorded

When:
- an IntroduceCard command executes for that entity with cost 3

Then:
- the command is accepted
- the entity's cost reads exactly 3
- the entity is flagged as a card (IsCard is true)
- the Command block closes as completed

Invariants:
- both mutations come from a single Execute, bracketed by one Command block
- a mutation of each tag value type (int and bool) lands through the same flow

Bound by: `Bindings/OneCommandAppliesSeveralMutations.cs`
