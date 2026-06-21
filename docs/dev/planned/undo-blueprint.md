# Design Notes: Undo Place Blueprint Feature

This document outlines the research and technical design for implementing an **Undo** feature for blueprint placements and copy-paste actions in the Blueprint Designer's Toolkit (BDT).

---

## Technical Feasibility & Architecture

To implement an undo feature, we need to track what changes are made when a player places a blueprint or pastes a selection, and then provide a way to revert those changes on demand.

### 1. Intercepting Blueprint & Paste Actions
During a copy-paste or blueprint placement, the game schedules several input commands to run on the simulation thread:
* `BatchCreateStaticEntitiesCmd`: Creates static entities and transport segments.
* `PasteSurfaceDesignationsCmd`: Pastes surface designations (concrete, asphalt, etc.).
* `BatchAddSurfaceDecalCmd`: Adds decals to surfaces.
* `ReplaceEntityCmd`: Upgrades/replaces existing buildings.

We can intercept these command invocations on the simulation thread using Harmony patches. Since the game engine executes commands sequentially on a single thread:
1. We patch the `Invoke` entry point of these commands (e.g., `EntitiesCommandsProcessor.Invoke(BatchCreateStaticEntitiesCmd)`) with a Harmony **Prefix** that initializes a transient `UndoRecord` and sets a thread-local or static active record reference: `s_activeUndoRecord = new UndoRecord()`.
2. We patch `EntitiesManager.TryAddEntity`, `SurfaceDesignationsManager.AddOrReplaceDesignation`, and decal addition methods. While `s_activeUndoRecord` is active, any added entity or modified designation tile is registered into that active record.
3. We patch the `Invoke` exit point with a Harmony **Postfix** to finalize the record and push it to a global `UndoStack` (capping the stack size, e.g. at 20 operations).

---

## How the Reversal (Undo) Works

When the user triggers the Undo hotkey (e.g., `Ctrl + Z`), BDT will pop the latest `UndoRecord` from the runtime queue and execute the following reversals:

### A. Reverting Placed Entities
For each recorded entity (static entities and transports) that was placed:
1. **If still under construction (Ghost):**
   * Call `m_constructionManager.StartDeconstruction(entity, doNotCreateProducts: true)`.
   * For static entities under construction, this cancels construction and instantly removes them from the world.
   * For transports, we can schedule `DeconstructTransportSegmentCmd` or call `m_transportsManager.TryDeconstructSubTransport` to remove the segment.
2. **If fully constructed (e.g., in Sandbox Mode with Instant Build):**
   * If in Sandbox / Instant Build mode, we can instantly delete the entity using `m_entitiesManager.RemoveAndDestroyEntityNoChecks(entity, EntityRemoveReason.Remove)`.
   * Otherwise, we initiate standard deconstruction to avoid cheating/exploits in survival play.

### B. Reverting Surface Designations & Decals
1. **Surface Designations:**
   * To revert concrete or other surface designations, BDT will schedule `RemoveSurfaceDesignationsCmd` (or `BatchRemoveSurfacePlacingDesignationsCmd`) containing the exact origin coordinates of the designations that were pasted.
2. **Decals:**
   * Reverted using `BatchRemoveSurfacePlacingDesignationsCmd` or equivalent decal-removal command.

### C. Restoring Overwritten Pre-existing Ghosts & Entities (e.g., Force Placement)
When placing a blueprint—especially when force-placing using **Shift-click**—pre-existing entities/ghosts in the target area may be deleted, deconstructed, or upgraded to make room. To ensure a true undo, BDT must restore these pre-existing objects:

1. **Tracking Overwritten Entities:**
   * While the placement commands are active and `s_activeUndoRecord` is set, we also intercept deconstruction and replacement commands (e.g., `StartDeconstructionOfStaticEntityCmd`, `StartDeconstructionOfTransportSubSectionsCmd`, and `ReplaceEntityCmd`).
   * For any entity deconstructed/removed, we capture its clone configuration using `ConfigCloneHelper.CreateConfigFrom(entity)` and record its original construction state (e.g., whether it was a ghost layout under construction or a fully completed building).
   * We store this original configuration in `s_activeUndoRecord.PreExistingEntities`.

2. **Reverting & Restoring Overwritten States:**
   * During the Undo action:
     * **If a pre-existing ghost layout was deleted:** We re-spawn it using `BatchCreateStaticEntitiesCmd` (with `applyConfiguration: true` to clone settings). Since the entity is new and has not had construction completed, it returns back to the planning thread as a ghost layout.
     * **If a fully constructed entity was marked for deconstruction but still exists:** We immediately cancel the deconstruction and restore it by calling `AbortDeconstruction()` (or scheduling `ToggleStaticEntityConstructionCmd`).
     * **If a fully constructed entity was completely demolished:** We re-create it, and if in Sandbox Mode, instantly complete its construction.
     * **If a transport segment was upgraded/replaced:** We schedule a `ReplaceEntityCmd` targeting the upgraded entity to restore it back to its pre-upgraded prototype and apply its original configurations.

---

## Compliance with Save Removability Rules

Per BDT's core architectural guidelines (`coi-maintained-mods.instructions.md`):
* The mod must remain safe to add to or remove from existing saves.
* **Transient Stack only:** The `UndoStack` is a purely in-memory runtime queue. It will **not** be serialized into save games. 
* Saving/loading the game will clear the undo queue. This ensures zero risk of save corruption, while remaining intuitive for users.
