# Transport Cleanup Tool

BDT can remove disconnected belts, pipes, lifts, balancers, connectors, and sorters from a selection dragged over the terrain.

## Hotkeys

- `Alt+Del`: arm transport cleanup mode by default
- `Esc` or right-click: cancel the armed mode

The tool hotkey can be changed in BDT's mod settings or seeded through `config.json`. In mod settings, click a keybinding field to edit it or right-click the field to clear it.

After arming the mode, drag a rectangle over the entities you want to check. Eligible entities highlight red, and the HUD lists their names and counts. Release the mouse over the world to remove the selection.

## Selection Rules

A belt or pipe is eligible if at least one end is disconnected. Drag over any part of it to select the whole segment. Lifts, balancers, connectors, and sorters are eligible if zero or one of their ports is connected.

This feature is destructive and changes real entities in the loaded game.
