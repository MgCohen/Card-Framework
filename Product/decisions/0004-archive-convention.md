---
type: adr
status: accepted
references: [_workflow/PRODUCT-BUILDER.md]
tags: [#workflow, #meta, #archive]
---

# ADR-0004 — Per-feature archive/ for superseded and cancelled docs

Date: 2026-05-01
Context-doc: `Product/_workflow/PRODUCT-BUILDER.md` §5.14
Proposal: `Product/_workflow/PRODUCT-BUILDER.md` §5.14

## Context

When reconciling the model spec we found a real bug by reading a superseded detail next to the new one. The old doc being co-located with the new one made the contradiction visible. Keeping retired docs inside their feature folder preserves that co-location property.

The risk of co-located retirement: a human scanning the folder is misled by stale content unless the retired doc is visually demoted. A flat `archive/` sibling at the same depth as `models/` and `decisions/` achieves both goals — co-located, demoted, not hidden.

A secondary benefit: archived docs become a structural lint signal. Any non-successor doc linking into `archive/` is a stale reference.

## Options considered

- **A — delete superseded docs**: Loses the historical record and the co-location win. Rejected.
- **B — leave retired docs in place with a status header**: No visual demotion; scanners are still misled. Rejected.
- **C — move to a project-wide `archive/` at Product root**: Breaks co-location; you can't read old and new together in one folder view. Rejected.
- **D — `archive/` sibling per feature (this decision)**: Co-located and demoted. Single folder covers both Superseded and Cancelled (a `superseded/` folder would require a second folder for cancellations). Chosen.

## Decision

Each feature folder gains an `archive/` subdirectory as a sibling of `models/` and `decisions/`. A doc moves into `archive/` only when its status flips to `Superseded` or `Cancelled` — as part of the same commit that flips the status. The archived doc carries four mandatory front-matter fields: `status`, `superseded_by` (iff Superseded), `archived_on`, `reason`. No `supersedes:` field on the active doc — the back-reference is synthesized on demand by `/builder:lint`. Four lint rules enforce the convention (see §5.14).

## Consequences

- Enables: historical record stays co-located; lint catches stale inbound links automatically; `archive/` is a reliable structural signal for migration audits.
- Constrains: any supersession or cancellation now requires a `git mv` into `archive/` plus the four front-matter fields — handled by `/builder:archive` (Tier 2 skill, see `OPEN-ITEMS.md`).
- Rollout: PR 1 (this ADR + convention + lint + skill updates); PR 2 (migrate existing superseded/cancelled docs); PR 3 (optional `/builder:archive` helper).
- Reverts/replaces: n/a (new convention; previously superseded docs stayed in place with only a status header).
