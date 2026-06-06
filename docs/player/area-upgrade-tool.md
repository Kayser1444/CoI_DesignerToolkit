# Area Upgrade Tool

BDT can upgrade or downgrade matching buildings inside a dragged screen rectangle.

## Hotkeys

- `Ctrl+PgUp`: arm area upgrade mode by default
- `Ctrl+PgDn`: arm area downgrade mode by default
- `Esc` or right-click: cancel the armed mode

The tool hotkeys can be changed in BDT's mod settings or seeded through `config.json`. In mod settings, click a keybinding field to edit it or right-click the field to clear it.

After arming a mode, drag a rectangle over buildings in the world. BDT immediately starts and finishes the matching upgrades or downgrades without materials, workers, or unity.

## Selection Rules

The tool only targets static entities that are constructed or still under construction. It skips entities that are being deconstructed, invalid, already upgrading, locked, unavailable, or unable to fit the target tier.

This feature is intended for design and testing workflows. It changes real entities in the loaded game.
