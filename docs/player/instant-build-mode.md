# Instant Build Mode

Instant build mode is a BDT setting that automatically completes construction and deconstruction work.

## Enable It

Open the BDT settings panel and turn on **Instant build mode** under **BUILD BEHAVIORS**.

When enabled, BDT checks the world after player commands are processed and immediately finishes static entities that are in construction or deconstruction. Surface placement and clearing designations also complete automatically in Sandbox, including while the simulation is paused.

## Interaction With Insta-Build

When instant build mode is enabled, BDT turns off the game's insta-build toggle. BDT uses its own construction/deconstruction completion pass instead of leaving the broader game cheat mode active.

Enable [Cut buildings](move-buildings.md) separately to cut completed buildings and pavement. With both settings enabled in Sandbox, BDT also accelerates the construction and demolition ordered by the Cut tool.

## Interaction With Planning Mode

Buildings placed while Planning Mode is enabled remain paused and are not completed by BDT. They are completed as soon as you unpause their construction, matching the game's normal Planning Mode behavior.

## Save Safety

The setting is stored as BDT config-backed state. BDT does not add custom game entities, prototypes, notifications, or save payloads for this feature, so the mod remains safe to remove from existing saves.
