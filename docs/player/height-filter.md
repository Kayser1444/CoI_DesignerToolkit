# Height Filter

The Height Filter allows players to adjust the visible rendering level of transports, transport pillars, and layout entities (such as sorters, zippers, mini-zippers, and lifts) in the world. This is highly useful for inspecting and managing multi-level logistics or underground layouts.

## Controls

- `PageUp`: increases the maximum visible level (renders higher levels, up to level 6 where everything is visible)
- `PageDown`: decreases the maximum visible level (hides higher levels, down to level 0 where only underground structures are visible)

These hotkeys can be customized in BDT's mod settings (under **HEIGHT FILTER**) or configured in `config.json`.

## Visible Levels

- **Level 0**: Underground structures and entities only.
- **Levels 1-5**: Shows entities up to that relative height level above terrain.
- **Level 6**: Shows all heights (default).

## Selection Behavior

Entities hidden by the height filter are protected from selection, preventing accidental interactions or demolition.
