# Commands

Turns a game action into validated, bracketed service calls on the server.

**Code:** `Assets/Commands/` · assembly `Framework.Commands` · depends on `Framework.Wire`

## What it does
- A command is a plain data class derived from `Command`.
- Each command type has exactly one `CommandHandler<T>`. A duplicate throws when the registry is built. A missing one throws on `Execute`.
- Each command type has at most one `CommandValidator<T>`. A validator is optional. It may read world state through services.
- `CommandService.Execute` validates first. A rejection returns `CommandResult.Reject(reason)` and emits nothing.
- An accepted command runs inside a `Command` block, so every event its handler causes sits between one `BlockStartEvent` and one `BlockEndEvent`.

## How it works
Server only. `CommandInstaller` registers nothing on the client.

1. Caller: `engine.Commands.Execute(cmd)`.
2. `CommandValidatorRegistry.TryFor(cmd.GetType())`. If a validator exists and returns false, return `Reject(reason)`. No event is emitted.
3. `BlockScope.Open(BlockKind.Command)` emits `BlockStartEvent`.
4. `CommandHandlerRegistry.HandlerFor(cmd.GetType()).Handle(cmd)`. The handler calls services, which emit events.
5. `BlockScope.Close(id, Completed)` emits `BlockEndEvent`. If the handler throws, the block closes as `Errored` and the exception propagates.
6. Return `CommandResult.Accept()`.

## Key types
| Type | Job |
|---|---|
| `Command` | Abstract base for command data. |
| `CommandHandler<T>` / `ICommandHandler` | Performs the command by calling services. |
| `CommandValidator<T>` / `ICommandValidator` | Accepts or rejects with a reason string. |
| `CommandHandlerRegistry` | Command type to handler. Built from the DI-collected list. |
| `CommandValidatorRegistry` | Command type to optional validator. |
| `CommandService` | `Execute`: validate, open block, handle, close block. |
| `CommandResult` | `Accepted` and `RejectReason`. |

## Example
From `Assets/Testing/Support/SampleCommands.cs`, registered with `builder.RegisterCommandHandler<DealDamageHandler>()`:

```csharp
public sealed class DealDamageHandler : CommandHandler<DealDamage>
{
    readonly TagService _tags;
    public DealDamageHandler(TagService tags) { _tags = tags; }
    public override void Handle(DealDamage cmd)
        => _tags.Set(cmd.EntityId, SampleTags.Health,
                     _tags.Get(cmd.EntityId, SampleTags.Health) - cmd.Amount);
}
```

## Gaps
- No hooks. Nothing runs before or after a command, so other rules cannot react to or replace one.
- Clients cannot submit commands. `Execute` is a server-side call, and no client to server path exists.
- A handler that throws midway leaves its earlier events applied. The block is marked `Errored`, but nothing rolls back. `WireEventHandler<T>.Unapply` throws `NotImplementedException`.
- `CommandResult` carries only a string reason. It does not return the block id or the events emitted.
