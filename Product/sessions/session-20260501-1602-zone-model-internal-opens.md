---
type: session
schema-version: 2
session-id: session-20260501-1602-zone-model-internal-opens
parent: session-20260501-1545-trigger-pipeline-shape
status: completed
started-at: 2026-05-01T16:02:00Z
last-updated: 2026-05-02
work-summary: "Design pass on zone model internal opens — settled 8 of 9 sub-questions, deferred spatial; locked cross-cutting Objects-over-IDs principle; iterated on zone/card shape (CardRef rotation, IReference, OwnershipState, CardMapState) reversing Q6.2 visibility lock. Patches applied: 5 ADRs (1 cross-cutting + 3 zone + 1 card), 1 new feature folder (cards), 2 Models substantially revised (zones, cards), 2 Models touched up (entities, glossary), open-questions/backlog updates, audit findings opened as alignment debt."
tags: [#session]
---

# session-20260501-1602-zone-model-internal-opens

## Part 1 — Active question coming in

**Zone model internal opens** (`oq-zone-internal-opens`) — nine questions within `zones/01-zone-model.md` have no follow-up model: zone-as-entity vs lightweight record, membership storage, actor/owner/controller semantics, visibility query API, forced insertion API, zone organisation query API, history of hidden movement, generated reward zones, spatial rule attachment. Several (membership storage, visibility API, actor semantics) are blocking for any non-trivial game. Design-pass candidate.

## Part 2 — Work done

Design pass + patch pass + design iteration on zone/card shape. The design pass settled 8 of 9 sub-questions plus the cross-cutting "Objects over IDs" principle. User opted to apply all cascading changes in the same session rather than defer. After the initial patch pass, a follow-up design iteration on the zone/card shape revisited several decisions:

- **Stripped speculative fields** — `SlotKey` and `GroupKey` removed from `ZoneContentsState`; `ContentEntry` collapsed to flat `IReadOnlyList<CardRef>`.
- **Rewrote `InsertPosition`** — replaced discriminated-union shape (mixed parameterless and parameterized variants on a single enum) with a sealed record hierarchy (`DefaultPosition | AtTop | AtBottom | AtIndex(int)`).
- **Designed the Card Model** — created a new feature folder `features/cards/` with Model + ADR. Card is `StateEntity<TCardDef>` (a class, per the entity bridge; not a record) implementing `IReference`. Lives in `CardMapState` keyed by `CardRef`. The map replaces the engine-side handle registry — putting registration on the Store gives snapshot/rollback for free.
- **Added `IReference`** — framework marker interface for objects that carry an identity in the `Identifier<Instance>` namespace. Zone implements directly; Card via `IReadOnlyEntity` ⊃ `IReference`. Polymorphic targeting takes `IReference`.
- **Owner via slice** — pulled Owner off the structural fields of Zone (and never put it on Card-as-class, since classes with init-only properties make field changes awkward). Generic `OwnershipState` slice keyed by `InstanceId`, applicable to any `IReference`.
- **Reversed Q6.2 visibility lock** — replaced per-actor transport alias with server-side `CardRef` rotation. Driven by the user's "play the same locally vs networked" principle: visibility filter cuts data, never alters identity. All observers see the same canonical `CardRef`; rotation on hide/show/move preserves privacy via the canonical state.
- **Renamed `RelocateInstanceMutation` → `MoveCardMutation`** — aligns with the established naming used in samples. `MoveCardMutation` carries `(Card, NewRef, Source, Destination, Position, BypassCapacity)`; every move rotates the `CardRef` per the conservative privacy policy.
- **Mutation payloads carry `NewRef`** — `Create`/`Move`/`Hide`/`Show` all take a fresh `CardRef` as a payload field. Ensures all sub-mutators within the composite see the same value and supports deterministic replay.

Patches applied this session:

1. **Created `Product/decisions/0005-objects-over-ids.md`** — cross-cutting framework ADR. Includes `IReference` as the framework marker.
2. **Created `Product/features/zones/decisions/0001-zone-shape.md`** — Zone implements `IReference`; state across `ZoneContentsState` (membership as `ImmutableList<CardRef>`), `OwnershipState` (owner), and per-zone-type runtime slices.
3. **Created `Product/features/zones/decisions/0002-visibility-model.md`** — two-channel visibility (membership + detail), definition-only zone policy, per-card enum `{ Hidden, VisibleToOwner, VisibleToAll }` on `CardVisibilityState`, server-side `CardRef` rotation (replacing original per-actor transport alias).
4. **Created `Product/features/zones/decisions/0003-insertion-api.md`** — single `InsertCardPayload { Zone, Card, Position, BypassCapacity }`; `InsertPosition` as sealed record hierarchy; static factories provide sugar.
5. **Created `Product/features/cards/models/01-card-model.md`** — Card is `StateEntity<TCardDef>` + `IReference`; `CardMapState` (`ImmutableDictionary<CardRef, Card>`) is the canonical "this card exists" home; `CardRef` rotates on every move and visibility transition; mutations (`Create`, `Move`, `Hide`, `Show`, `Destroy`); read API. (Initially included `CardLocationIndex` aggregate, removed in cleanup pass — no current consumer.)
6. **Created `Product/features/cards/decisions/0001-card-shape.md`** — locks Card-as-`StateEntity`-class, two-ID model (`InstanceId` stable + `CardRef` ephemeral), `CardMapState` replacing engine-side handle registry, mutations carrying `NewRef`.
7. **Revised `Product/features/zones/models/01-zone-model.md`** — full revision matching the Card Model; Zone class signature with `IReference`; `ZoneContentsState` as `ImmutableList<CardRef>`; Owner via `OwnershipState`; visibility section rewritten for server-side `CardRef` rotation; `MoveCardMutation` cross-references; spatial deferred section noted.
8. **Touched up `Product/features/entities/models/01-definition-instance-model.md`** — clarified that `StateEntity` is a class (not a record); for cards, the registry lives on the Store as `CardMapState` (replaces engine-side registry); cross-link to Card Model added; zone-related Next Questions struck with resolution links.
9. **Updated `Product/ledgers/GLOSSARY.md`** — added Card, CardRef, CardMapState, IReference, OwnershipState, CardVisibilityState; refined Actor (framework base), Player (subtype of Actor), Owner (named concept), Zone (thin handle implementing IReference), Identifier<T> (boundary-only), Instance ID (stable, distinct from CardRef), InsertCardPayload (sealed-record InsertPosition), ZoneContentsState (ImmutableList<CardRef>); renamed RelocateInstanceMutation → MoveCardMutation with same-row update; deprecated Ephemeral Instance ID; Conventions section names CardRef-vs-InstanceId discipline.
10. **Updated `Product/ledgers/OPEN-QUESTIONS.md`** — struck `oq-zone-internal-opens` with resolution note linking to all ADRs.
11. **Updated `Product/ledgers/BACKLOG.md`** — added `bl-spatial-rule-attachment`.
12. **Audit findings opened as alignment-debt** — `ALIGNMENT-DEBT.md` got 8 entries (`ad-20260501-040` through `ad-20260501-047`) covering the broader Objects-over-IDs audit across `effects/`, `engine/`, `actions/`, `presentation/`, and snapshot pipeline. Each Model needs focused per-Model alignment beyond this session's safe scope.

Net deltas: 5 new ADRs (1 cross-cutting + 3 zone + 1 card) · 1 new feature folder (cards) · 2 Models substantially revised (zones, cards) · 1 Model touched up (entities) · 1 glossary substantially expanded · 1 open-question strike · 1 backlog add · 8 alignment-debt entries opened.

## Part 3 — Decisions settled

- **Q1 = Zone shape** — Zone is a class implementing `IReference`; structural fields `(Id, Definition)`; state in `ZoneContentsState` (membership) + `OwnershipState` (owner) + per-zone-type runtime slices. Shared `Identifier<Instance>` namespace with cards. No entity-bridge / modifier-stack machinery. Stat-bearing things are not zones.
- **Q2 = Contents storage** — generic framework-owned `ZoneContentsState` slice as flat `ImmutableList<CardRef>`. Per-zone-type runtime slices for typed values. Slot/group/spatial structure deferred — games add fields when forced.
- **Q3 = Terminology** — Actor is framework base ("anything that can own instances"); Player is a kind/subtype of Actor; Owner is a named concept (the Actor that owns an instance) implemented via `OwnershipState` slice. Controller and Viewer are dropped.
- **Cross-cutting = Objects over IDs** — framework APIs prefer domain objects (`Zone`, `Card`, `StateEntity`, `Player`); `Identifier<T>` reserved for boundaries. `IReference` is the framework marker for objects with InstanceId. ADR-0005.
- **Q4 = Read API** — `Zone` is a thin handle (class) used directly as slice key (`state.Get<X>(zone)`); utility methods take state per call (`zone.GetCards(state)`); no internal scope reference. Reverse-lookup aggregates deferred to backlog (`bl-card-location-index`).
- **Q5 = Forced insertion** — single `InsertCardPayload { Zone, Card, Position, BypassCapacity }`; `Position` is a sealed record hierarchy (`DefaultPosition | AtTop | AtBottom | AtIndex(int)`); static factories for sugar; `MoveCardMutation` forwards same options.
- **Q6.1 = Visibility readability** — rules read via `card.IsVisibleTo(state, actor)`; flag on `CardVisibilityState` slice keyed by `InstanceId`.
- **Q6.2 = Privacy mechanism (REVISED)** — server-side `CardRef` rotation. Cards carry stable `InstanceId` + ephemeral `CardRef`; `CardRef` rotates server-side on every visibility transition (and per ADR-0003, every move). All observers see the same canonical ref. Original transport-alias mechanism reversed per the "play the same locally vs networked" principle.
- **Q6.3 = Zone visibility policy** — definition-only; runtime variation comes exclusively from per-card flag.
- **Q6.4 = Per-card visibility flag** — enum `{ Hidden, VisibleToOwner, VisibleToAll }`; absent entry = use zone policy default.
- **Q7 = History filtering** — single canonical history server-side. Transport applies detail gating only — card-detail entries (variable changes, modifier changes) gated on detail visibility; movement and ref-rotation entries always sent. No per-actor ID translation needed (all observers see canonical refs).
- **Q8 = Generated content** — rewards/offers/drafts are decisions, not zones; card packs are entities with generator effects.
- **Q9 = Spatial** — **deferred.** Framework does not pre-define a hook (no `SlotKey?` field). Games add the appropriate fields when forced. New backlog entry.
- **Card shape (added during iteration)** — Card is `StateEntity<TCardDef>` (a class, not a record) implementing `IReference`. Lives in `CardMapState` (`ImmutableDictionary<CardRef, Card>`). Two IDs: stable `InstanceId` (variable storage, modifier storage, ownership, history, server-internal subscriptions) + ephemeral `CardRef` (zone contents, client-visible state, rotates). Mutations carry `NewRef` to ensure determinism. ADR `features/cards/decisions/0001-card-shape.md`.
- **Owner storage (added during iteration)** — generic `OwnershipState` slice keyed by `InstanceId`, applicable to any `IReference`. Read via `card.GetOwner(state)` / `zone.GetOwner(state)`. Cards/zones without an owner have no entry; OwnerOf returns null.
- **MoveCardMutation naming (added during iteration)** — replaces `RelocateInstanceMutation` (which was glossary-only); aligns with samples. Carries `(Card, NewRef, Source, Destination, Position, BypassCapacity)`; rotates `CardRef` on every move per the conservative privacy policy.

## Part 4 — Outcome

**Two audit passes completed in-session.** After landing the Card Model + Q6.2 reversal commit, ran a critical review and surfaced 15 issues (Q1–Q15 in the first audit). Walked Q-by-Q, locked verdicts, applied the cleanup cascade. Then ran a second audit pass over the resulting state — surfaced 9 more issues (real bugs, stale phrasing, leftover overreach). Walked the discussion items (F, G, H), locked verdicts, applied a second cleanup pass.

**Second-audit fixes:**
- **A**: `InsertPosition Position = default` would resolve to null, not `DefaultPosition` — changed to `InsertPosition? Position = null` with explicit null-handling in mutator semantics. Both cards Model (`CreateCardMutation`), zone Model (`InsertCardPayload`), and zones ADR-0003 fixed.
- **B**: Duplicate `GetCards` definitions (extension method in cards Model + method on Zone class). Removed the extension; cards Model now points at `Zone.GetCards`.
- **C**: Cards Model intro had stale `StateEntity<TCardDef>` — changed to `StateEntity<CardDefinition>` (Q2 lock).
- **D**: Zone Model § Model Summary had Owner in the formula and prose — escaped first-pass cleanup. Removed.
- **E**: Cards Model "Card-typed card variants" stutter — reworded to "Card variants."
- **F**: Dropped `InsertCardPayload.Force` factory. Use `with { BypassCapacity = true }` everywhere. No symmetric `Force` factories on `MoveCardMutation` either.
- **G**: Fixed `store.State.Get<X>()` → `store.Get<X>()` (canonical Scaffold API per effects/04).
- **H**: Dropped `bl-zone-registry` backlog entry. The user's framing was "we don't need this; trust Scaffold," not "deferred until needed." Applied the discipline established for `CardLocationIndex`.
- **I**: Refined Useful Invariants line about history-CardRef — distinguishes movement entries (CardRef) from server-internal identity entries like `CardRefRotated` (InstanceId).

Audit fixes applied:
- **Q1 RefOf O(n)** — added parallel `Dictionary<Card, CardRef>` reverse index in `CardMapState`; Card overrides `Equals`/`GetHashCode` by `InstanceId`.
- **Q2 Card non-generic** — kept Card non-generic (data-driven); removed `Fireball : Card` example and the "or `StateEntity<TConcreteDef>` directly" admission.
- **Q3 Hide/Show asymmetry** — collapsed `HideCardMutation` + `ShowCardMutation` into `SetCardVisibilityMutation` with `Hide` / `ShowAll` / `ShowToOwner` factories.
- **Q4 ZoneMapState gap** — explicit decision: no `ZoneMapState`; zones are game-authored. Added `bl-zone-registry` to backlog with trigger condition.
- **Q5 zone lifecycle** — auto-resolved by Q4 (game-authored).
- **Q6 InstanceId allocation** — `InstanceId.NewId()` factory documented; entities Model updated.
- **Q7 Card construction** — `CreateCardMutation` now takes `Definition` (not Card); framework constructs Card inside the composite; `Store.CreateCard(definition, intoZone?)` helper for sugar.
- **Q8 Zone Model line 89 stale** — reworded to clarify Owner is via `OwnershipState`.
- **Q9 + Q10 CardVisibilityState/CardVisibility location** — moved from zones ADR-0002 to cards Model § Card Visibility; zones ADR cross-references.
- **Q11 ADR-0002 in-place edit** — added §5.14 addendum to PRODUCT-BUILDER documenting pre-merge ADR mutability carve-out (archive convention applies post-merge).
- **Q12 Owner phrasing** — reworded Card Model summary: "state-side accessors" not "state-side properties."
- **Q13 factory naming** — no change (over-flagged in audit).
- **Q14 move validation drift** — strict validation (throw); opened `oq-move-validation-execution-drift` to track the deferred concurrency question.
- **Q15 CompositeAction trigger fold** — added one-liner to cards Model § Mutations noting mutations pass through trigger fold.

**Active question going out (after this audit):** Discharge the 8 Objects-over-IDs alignment-debt entries (`ad-20260501-040` through `ad-20260501-047`) opened earlier this session. Plus `oq-move-validation-execution-drift` opened in Q14 — to be revisited when concurrency pain surfaces.

**Other deltas to track:**
- 5 ADRs total (1 cross-cutting + 3 zone + 1 card).
- Zone Model + Card Model are both `last-updated: 2026-05-01`; lint pass at convenient time recommended.
- Backlog: `bl-spatial-rule-attachment` + `bl-card-location-index` + `bl-zone-registry`.
