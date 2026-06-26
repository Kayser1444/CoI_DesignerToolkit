# Undo Place Blueprint

BDT adds an in-memory undo stack for recent blueprint placement and copy/paste actions.

## Hotkeys

- `Ctrl+Z`: undo the most recent supported placement action by default

The hotkey can be changed in BDT's mod settings or seeded through `config.json`.

## Supported Actions

Undo can revert recent blueprint placement, copy-paste, and force-placement actions.

Depending on what the placement did, BDT can:

- cancel newly placed ghosts
- start deconstruction for newly placed fully built structures
- immediately destroy newly placed structures in sandbox mode
- restore overwritten pre-existing ghosts or entities
- revert pasted surface designations and decals

## Limitations

Undo history is transient runtime state only. It is not saved into the game save file and is cleared when the game/session state is rebuilt.

The feature is intended as a safety net while designing. It is not a general-purpose world history system.
