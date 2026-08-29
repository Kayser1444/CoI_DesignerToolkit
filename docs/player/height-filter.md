# Height Filter

The Height Filter allows players to adjust the visible rendering level of transports, transport pillars, and layout entities (such as sorters, zippers, mini-zippers, and lifts) in the world. This is highly useful for inspecting and managing multi-level logistics or underground layouts.

## Controls

- `PageUp`: increases the maximum visible level (renders higher levels, up to level 6 where everything is visible)
- `PageDown`: decreases the maximum visible level (hides higher levels, down to level 0 where only underground structures are visible)
- `Shift+PageUp`: steps the transport visibility policy towards **High**
- `Shift+PageDown`: steps the transport visibility policy towards **Low**

These hotkeys can be customized in BDT's mod settings under **HEIGHT FILTER** or in the vanilla **Settings | Controls** menu.

## Visible layers

- **Level 0**: Underground structures and entities only.
- **Levels 1-5**: Shows entities up to that relative height level above terrain.
- **Level 6**: Shows all heights (default).

## Transport visibility

The **Transport visibility** policy controls how a transport's inflection points are compared with the active height threshold:

- **Low**: All inflection points must be at or below the threshold for the transport to remain visible.
- **Medium** (default): A majority of inflection points must be at or below the threshold for the transport to remain visible.
- **High**: At least one inflection point must be at or below the threshold for the transport to remain visible.

## Pillar Visibility

The **Pillar visibility** setting under **HEIGHT FILTER** controls how support pillars respond to height filtering:

- **Detached** (default): Each pillar independently evaluates its own vertical segments against the active layer threshold (a pillar is hidden if 50% or more of its segments exceed the visible level).
- **Attached**: A pillar remains visible whenever at least one visible transport or elevated structure is attached to it.

## Selection Behavior

Entities hidden by the height filter are protected from selection, preventing accidental interactions or demolition.
