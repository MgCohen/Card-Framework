# Systems

One short page per system: what it does, how it works, its key types and its known gaps. For how the systems fit together, read [architecture.md](../architecture.md).

## Framework (current code)

Each feature lives in `Assets/<Feature>/` as assembly `Framework.<Feature>`.

| System | What it is for |
|---|---|
| [Core](core.md) | Typed slot ids and the registry that rejects slot clashes. |
| [Wire](wire.md) | Event stream, apply-handler dispatch, blocks, network channels and transports. |
| [Tags](tags.md) | Unmanaged values per (entity, tag slot), synced to clients through events. |
| [Entities](entities.md) | Entity ids, entity types and the link to a card definition. |
| [States](states.md) | The current match state. |
| [Requests](requests.md) | The server asks a player for a decision and awaits a typed response. |
| [Commands](commands.md) | Validate, then handle, inside a command block. Server only. |
| [Engine](engine.md) | Server handle to the services and read-only store views. |
| [Bootstrap](bootstrap.md) | VContainer composition root and the per-feature installers. |
| [Multiplayer](multiplayer.md) | Lobby, peers and ready-up over Netcode for GameObjects. |
| [Testing](testing.md) | The global acceptance and integration test tiers. |

## Deleted spikes (reference shapes)

These prototypes were removed from the tree. Their code is still in git at commit `b13907e`. Each page records how the spike worked, the code shapes worth reusing and how it differs from the Framework. Read them as ideas, not as code to copy: most of them change state first and log it after, the reverse of the Framework.

| Spike | What it explored |
|---|---|
| [VerticalSlice](spike-vertical-slice.md) | A complete game loop: commands, effects, hooks, the stack, state-based actions, zones and a replay mirror. |
| [StateMachine](spike-state-machine.md) | The game as a state machine of stateless states. |
| [Modifiers](spike-modifiers.md) | Modifiers as entities. |
| [TagAuthoring](spike-tag-authoring.md) | Tags defined as ScriptableObjects by designers. |
| [CardPresentation](spike-card-presentation.md) | Card and zone views: view models, drag and drop, zone layouts. |
