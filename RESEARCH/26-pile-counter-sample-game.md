# Pile Counter — Reference Sample Game

This document defines a small, deliberately simple game we use as a working reference while shaping the framework. Whenever a framework concept feels under- or over-specified, we sketch its implementation in **Pile Counter** and see where the seams are.

The game has no triggers, no abilities, no players-vs-players, no async decisions. That's intentional — it's the floor of what the framework must support, before any of the harder features pile on.

---

## 1. Concept

A single-player puzzle. The deck is split across four visible piles; the player slides cards between piles, trying to make each pile's total score equal its target.

---

## 2. Components

### Card

- **Number** — integer in `[1..10]`.
- **Image** — purely cosmetic; the framework treats it as opaque.

The deck contains 10 cards. (Whether it's exactly the numbers 1..10 or any 10 cards in that range is a setup variant — see open questions.)

### Pile (Zone)

- **Stack semantics** — cards are stacked; only the top card is visible to the player.
- **Score** — the sum of every card's `Number` in the pile. Visible.
- **Target** — a fixed integer assigned at setup. Visible.
- **Top card** — the most recently added card. Its `Number` is visible; cards beneath are hidden.

There are exactly four piles. They are positional/cosmetic — no pile is special by identity.

### Game

- 10 cards, 4 piles, 4 target scores.
- No turn timer, no move limit, no opponent.

---

## 3. Setup

1. Build the deck (10 cards).
2. Shuffle.
3. Distribute cards across the four piles. Distribution is roughly even but **10 doesn't divide evenly into 4** — see open questions.
4. Assign each pile a target score. Targets are chosen so the puzzle is solvable from the dealt configuration. (How targets are generated is out of scope — assume a puzzle generator hands them in.)
5. Compute initial pile scores (sum of `Number`s per pile).
6. Game enters its main loop.

---

## 4. Player Actions

There is exactly one action: **MoveTopCard**.

- **Input:** source pile, destination pile.
- **Legality:**
  - Source pile must be non-empty.
  - Source ≠ destination.
  - The card being moved (the top of the source) must not be the card moved on the immediately previous move. ("No moving the same card twice in a row.")

That is the entire action surface. No other player input exists during play.

---

## 5. Game Loop

```
loop:
    player picks (source, destination)
    if move illegal → reject, no state change
    else:
        top card of source moves to top of destination
        recompute scores for source and destination
        if all four pile scores equal their targets → win, game ends
        else → continue
```

No phases beyond "play until won." No automatic advancement, no idle effects.

---

## 6. Win Condition

After a move resolves, if `pile.Score == pile.Target` for **all four piles** simultaneously, the game ends and the player wins.

There is no lose condition. The puzzle is always solvable from setup; the player keeps moving until they hit it.

---

## 7. What Pile Counter Tests In The Framework

This game forces the framework to express:

- **Zones with stack ordering and a derived score.** The score is a *computed* per-zone value — the framework needs a way to expose this without forcing the game author to recompute it manually after every change. (Or to recompute manually but cleanly.)
- **A single action type with legality rules.** Tests `OnIncomingCommand` rejection, `AvailableActions` (the gateway needs to tell the UI which moves are legal), and how legality interacts with the "can't move same card twice in a row" memory.
- **State-based win check.** Classic `Evaluate` job: after each action, read state, decide if the game is over, dispatch an `EndGameCommand` (or equivalent).
- **Move history of size 1.** "Last card moved" is a sliver of state that doesn't fit on a card or a zone — it's *game-level* memory. Where does it live?

This game does **not** test:

- Triggers, abilities, replacement effects, ongoing effects.
- Async decisions (no targeting, no choice prompts).
- Multi-player or priority.
- Phase transitions.
- Card definitions vs. instances (cards are pure data — number + image).

We will revisit those features against more elaborate sample games once the floor is solid.

---

## 8. Open Questions To Resolve Before Implementing

These are ambiguities in the rules above that affect the framework sketch. Resolve before writing the `GameRules` implementation against this game.

- **10 ÷ 4 distribution.** Two piles of 3 + two piles of 2? Some other split? Configurable?
- **Card identity for "twice in a row" rule.** Is it the physical card instance, or the card *number*? E.g. if two cards both have `Number = 5`, does moving one immediately after the other count as the same card?
- **Card numbering.** Is the deck always exactly `[1..10]`, or 10 cards with numbers freely chosen in `[1..10]` (potentially repeating)? This affects whether numbers are unique.
- **Target assignment.** Are targets generated alongside the shuffle (puzzle generator), or fixed by the level? For sketching the framework, assume "given as setup input."
- **Empty piles.** Can a pile become empty? (Yes, by repeated moves out of it.) Is an empty pile's score 0? Does it still have a target? Is winning possible with an empty pile (target = 0)?
- **Undo.** Out of scope; assume not supported.
- **Restart / new puzzle.** Out of scope for the GameRules sketch; treat as a session-lifecycle concern.

---

## 9. How This Doc Is Used

When designing or revisiting a framework piece, sketch its Pile Counter implementation alongside the framework type. If the sketch is awkward, the framework is wrong. If the sketch is trivial, the framework is at least adequate for the floor.

The first use is the `GameRules` shape (doc 25): we will write Pile Counter's `GameRules` and see whether the four-method interface plus `IntakeContext` covers the game cleanly.
