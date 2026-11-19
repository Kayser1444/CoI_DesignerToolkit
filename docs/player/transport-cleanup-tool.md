# Transport Cleanup Tool

BDT can remove disconnected belt and pipe segments inside a selection dragged over the terrain.

## Hotkeys

- `Alt+Del`: arm transport cleanup mode by default
- `Esc` or right-click: cancel the armed mode

The tool hotkey can be changed in BDT's mod settings or seeded through `config.json`. In mod settings, click a keybinding field to edit it or right-click the field to clear it.

After arming the mode, drag a rectangle over belts or pipes in the world. Matching disconnected transport segments highlight red while selected, then BDT removes them when the mouse is released over the world.

## Selection Rules

The selection matches any part of a disconnected belt or pipe. The tool removes the whole matching segment. Connected belts and pipes are skipped.

This feature is destructive and changes real entities in the loaded game.
