# Testing

Holds the global test tiers and the shared fixtures they use. The rules live in [docs/testing.md](../testing.md).

**Code:** `Assets/Testing/` · assemblies `Framework.Testing.Support`, `Framework.Testing.Acceptance.Tests`, `Framework.Testing.Integration.Tests` · Support depends on `Framework.Bootstrap`, `Framework.Commands`, `Framework.Core`, `Framework.Wire`, `Framework.Tags`, `Framework.Entities`, `Framework.Engine`, `VContainer`

## What it does
- **Support** gives both global tiers one way to start a match, plus shared sample commands and tags. It holds no tests.
- **Acceptance** proves gameplay behaviour. Each test is a prose picture in `Acceptance/Scenarios/` bound to an NUnit test in `Acceptance/Bindings/`.
- **Integration** proves wiring and composition. Tests are hand-written and may touch internals.
- Per-feature **unit** tests stay in each feature's own `Tests/` folder, not here.
- Asmdef references are the fence. Acceptance cannot see Requests, States or any internals.

## How it works
1. A test calls `MatchHarness.Start(bind)`. It installs a server `GameplayLayer` plus the test's registrations into one scope and resolves `GameEngine`.
2. Or a test calls `MatchPair.Start(bind)`. It builds a server scope and a client scope joined by `LoopbackTransport.Pair`. `bind` runs once per side.
3. The test drives the server with `match.Execute(cmd)` or `match.Engine.*`.
4. It asserts on the server stores. With `MatchPair` it also reads the client stores and calls `AssertStreamsMatch()`, which compares every event field by field.
5. `EngineStack` (Integration) builds the same server graph by hand, without VContainer.

## Key types
| Type | Job |
|---|---|
| `MatchHarness` | Server-only match on the real composition root. `Execute`, `Engine`. |
| `MatchPair` | Server and client over loopback. `Server`, `Client`, `AssertStreamsMatch`. |
| `PictureAttribute` | `[Picture("x.md")]` links a binding to its picture. |
| `SampleCommands` (file) | `SetHealth`, `DealDamage`, `IntroduceCard` with handlers and validators. |
| `SampleTags` | `Health`, `Cost`, `IsCard` on slots 2001 to 2003. |
| `EngineStack` | Integration. Hand-wired server engine plus DrawCard request handlers. |
| `DrawCardRequest`, `DrawCardResponse` + handlers | Integration. Request fixtures the Requests feature does not ship. |

## Example
From `Assets/Testing/Integration/TwoSidedMatchIntegrationTests.cs`:

```csharp
using var match = StartWithDamageCommands();

var unit = match.Engine.Entities.Create(new Card());
Assert.IsTrue(match.Execute(new SetHealth { EntityId = unit.Id, Value = 30 }).Accepted);
Assert.IsTrue(match.Execute(new DealDamage { EntityId = unit.Id, Amount = 12 }).Accepted);

var clientTags = match.Client.Resolve<TagStore>();
Assert.AreEqual(18, clientTags.Get<int>(unit.Id, SampleTags.Health.Slot));
match.AssertStreamsMatch();
```

## Gaps
- The four pictures cover commands and tags on the server only. No picture covers states, requests or the client.
- Acceptance cannot reach States or Requests at all, because its asmdef fence excludes them.
- `DrawCardResponseHandler` calls `RequestService.Complete` from an apply handler. ARCH-06 allows it only because `Framework.Testing.*` is exempt.
- The tree in `docs/testing.md` omits `TwoSidedMatchIntegrationTests.cs`.
